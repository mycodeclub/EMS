using EMS.Data;
using EMS.Models;
using EMS.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace EMS.Services.People;

/// <summary>Who completes a checklist item: HR on the employee page, or the employee in self-service.</summary>
public enum ChecklistOwner { Hr = 1, Employee }

/// <summary>One onboarding step. Document items carry the type and the latest upload's status.</summary>
public record ChecklistItem(string Label, bool Done, string? Note, ChecklistOwner Owner, DocumentType? Document = null, DocumentStatus? DocumentStatus = null);

public record Checklist(IReadOnlyList<ChecklistItem> Items)
{
    public int Done => Items.Count(i => i.Done);
    public int Total => Items.Count;
    public bool IsComplete => Done == Total;
    public int Percent => Total == 0 ? 100 : Done * 100 / Total;
    public int DocumentsToVerify => Items.Count(i => i.DocumentStatus == Models.DocumentStatus.Pending);
}

/// <summary>
/// The joining checklist: details the employee fills in (personal details, PAN, Aadhaar, bank account), the shift HR
/// assigns, and the documents HR must verify. A previous employer's relieving letter is needed only when the employee has
/// prior experience. A login is optional (not every employee uses EMS), so it is not on the list.
/// </summary>
public class OnboardingChecklist(ApplicationDbContext db)
{
    public static IReadOnlyList<DocumentType> RequiredDocuments(Employee e) =>
        e.PriorExperienceMonths > 0
            ? [DocumentType.PanCard, DocumentType.AadhaarCard, DocumentType.EducationCertificate, DocumentType.BankProof, DocumentType.PassportPhoto, DocumentType.PreviousRelievingLetter, DocumentType.SignedOfferLetter]
            : [DocumentType.PanCard, DocumentType.AadhaarCard, DocumentType.EducationCertificate, DocumentType.BankProof, DocumentType.PassportPhoto, DocumentType.SignedOfferLetter];

    public async Task<Checklist> ForAsync(Employee employee, CancellationToken ct = default) =>
        Build(employee, await db.EmployeeDocuments.Where(d => d.EmployeeId == employee.UniqueId).ToListAsync(ct));

    public async Task<Dictionary<int, Checklist>> ForAsync(IReadOnlyCollection<Employee> employees, CancellationToken ct = default)
    {
        var ids = employees.Select(e => e.UniqueId).ToList();
        var documents = (await db.EmployeeDocuments.Where(d => ids.Contains(d.EmployeeId)).ToListAsync(ct)).ToLookup(d => d.EmployeeId);
        return employees.ToDictionary(e => e.UniqueId, e => Build(e, documents[e.UniqueId]));
    }

    public static Checklist Build(Employee e, IEnumerable<EmployeeDocument> documents)
    {
        var missing = new[]
        {
            (e.DateOfBirth is null, "date of birth"), (string.IsNullOrEmpty(e.Mobile), "mobile"),
            (string.IsNullOrEmpty(e.CurrentAddress), "address"), (string.IsNullOrEmpty(e.EmergencyContactPhone), "emergency contact"),
        }.Where(x => x.Item1).Select(x => x.Item2).ToList();

        var items = new List<ChecklistItem>
        {
            new("Personal details", missing.Count == 0, missing.Count == 0 ? null : $"Missing: {string.Join(", ", missing)}", ChecklistOwner.Employee),
            new("PAN number", e.Pan is not null, null, ChecklistOwner.Employee),
            new("Aadhaar number", e.Aadhaar is not null, null, ChecklistOwner.Employee),
            new("Salary bank account", e.BankAccountNumber is not null && e.BankIfsc is not null, null, ChecklistOwner.Employee),
            new("Shift assigned", e.ShiftId is not null, null, ChecklistOwner.Hr),
        };

        var latest = documents.GroupBy(d => d.Type).ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.UniqueId).First());
        foreach (var type in RequiredDocuments(e))
        {
            var doc = latest.GetValueOrDefault(type);
            var note = doc?.Status switch
            {
                DocumentStatus.Verified => null,
                DocumentStatus.Pending => "Uploaded, waiting for HR to verify",
                DocumentStatus.Rejected => $"Rejected{(doc.ReviewNote is null ? "" : $": {doc.ReviewNote.TrimEnd('.')}")}. Upload it again.",
                _ => "Not uploaded",
            };
            items.Add(new(type.DisplayName(), doc?.Status == DocumentStatus.Verified, note, ChecklistOwner.Employee, type, doc?.Status));
        }
        return new Checklist(items);
    }
}
