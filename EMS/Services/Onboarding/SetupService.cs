using System.Text.RegularExpressions;
using EMS.Data;
using EMS.Models;
using EMS.Models.Onboarding;
using Microsoft.EntityFrameworkCore;

namespace EMS.Services.Onboarding;

/// <summary>Shift master and employee setup shared by the onboarding wizard and the organization admin panel.</summary>
public partial class SetupService(ApplicationDbContext db)
{
    public Task<List<Shift>> ShiftsAsync(int organizationId, CancellationToken ct = default) =>
        db.Shifts.Where(s => s.OrganizationId == organizationId).OrderBy(s => s.StartTime).ToListAsync(ct);

    /// <summary>Replaces the organization's shifts with <paramref name="rows"/>: matching ids are updated, new rows added, missing ones deleted.</summary>
    public async Task<string?> SaveShiftsAsync(Organization organization, bool operatesInShifts, IReadOnlyList<ShiftRow> rows, CancellationToken ct = default)
    {
        var existing = await ShiftsAsync(organization.UniqueId, ct);
        // Existing codes stay reserved, so a new shift never takes the code of one deleted in the same save.
        var codes = existing.Select(s => s.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed = existing.ToList();

        foreach (var row in rows)
        {
            var shift = existing.FirstOrDefault(s => s.UniqueId == row.Id);
            if (shift is null)
            {
                shift = new Shift { OrganizationId = organization.UniqueId, Code = UniqueCode(row.Name, codes) };
                db.Shifts.Add(shift);
            }
            removed.Remove(shift);

            var worked = (int)(row.EndTime - row.StartTime).TotalMinutes - row.BreakMinutes; // TimeOnly subtraction wraps past midnight
            shift.Name = row.Name.Trim();
            shift.StartTime = row.StartTime;
            shift.EndTime = row.EndTime;
            shift.BreakMinutes = row.BreakMinutes;
            shift.GraceMinutes = 15;
            shift.FullDayMinutes = Math.Max(0, worked);
            shift.HalfDayMinutes = Math.Max(0, worked / 2);
            shift.IsActive = true;
        }

        db.Shifts.RemoveRange(removed);
        organization.OperatesInShifts = operatesInShifts;
        return await TrySaveAsync(ct);
    }

    /// <summary>Adds the non-blank rows to the head office. Blank employee codes get the next free code (E001, E002…).</summary>
    public async Task<(int Added, string? Error)> AddEmployeesAsync(Organization organization, IEnumerable<EmployeeRow> rows, CancellationToken ct = default)
    {
        var branch = OrganizationContext.DefaultBranch(organization);
        var codes = await EmployeeCodesAsync(organization.UniqueId, ct);
        var shiftIds = (await ShiftsAsync(organization.UniqueId, ct)).Select(s => s.UniqueId).ToHashSet();
        var added = 0;

        foreach (var row in rows.Where(r => !r.IsBlank))
        {
            if (row.ShiftId is { } shiftId && !shiftIds.Contains(shiftId)) return (0, "Choose one of your own shifts for each employee.");

            var code = string.IsNullOrWhiteSpace(row.EmpCode) ? NextCode(codes) : row.EmpCode.Trim().ToUpperInvariant();
            if (!codes.Add(code)) return (0, $"Employee code {code} is already used. Codes are never reused, even after an employee is removed.");

            db.Employees.Add(new Employee
            {
                OrganizationId = organization.UniqueId,
                BranchId = branch.UniqueId,
                EmpCode = code,
                FirstName = row.FirstName!.Trim(),
                LastName = row.LastName?.Trim(),
                Mobile = row.Mobile?.Trim(),
                Designation = row.Designation?.Trim(),
                DateOfJoining = row.DateOfJoining ?? DateOnly.FromDateTime(AppClock.Today),
                ShiftId = row.ShiftId,
                MonthlySalary = row.MonthlySalary,
            });
            added++;
        }

        var error = await TrySaveAsync(ct);
        return error is null ? (added, null) : (0, error);
    }

    /// <summary>All codes ever used in the organization, including removed employees.</summary>
    public async Task<HashSet<string>> EmployeeCodesAsync(int organizationId, CancellationToken ct = default) =>
        (await db.Employees.IgnoreQueryFilters([ApplicationDbContext.SoftDeleteFilter])
            .Where(e => e.OrganizationId == organizationId).Select(e => e.EmpCode).ToListAsync(ct))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string NextCode(HashSet<string> used)
    {
        var next = used.Select(c => EmpCodeNumber().Match(c)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max() + 1;
        while (used.Contains($"E{next:000}")) next++;
        return $"E{next:000}";
    }

    private static string UniqueCode(string name, HashSet<string> used)
    {
        var letters = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var stem = letters.Length == 0 ? "SH" : letters[..Math.Min(3, letters.Length)];
        var code = stem;
        for (var i = 2; !used.Add(code); i++) code = $"{stem}{i}";
        return code;
    }

    private async Task<string?> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (BusinessRuleException ex)
        {
            db.ChangeTracker.Clear();
            return ex.Message;
        }
    }

    [GeneratedRegex(@"^E(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex EmpCodeNumber();
}
