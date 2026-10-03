using System.Security.Claims;
using EMS.Data;
using EMS.Models;
using Microsoft.EntityFrameworkCore;

namespace EMS.Services.Onboarding;

public enum OnboardingStep { Password = 1, Profile, Shifts, Employees, Done }

/// <summary>
/// The signed-in user's organization (loaded once per request): the one they own (OrgAdmin), or the one whose employee
/// record is linked to their login (HR, Accounts, Employee).
/// </summary>
public class OrganizationContext(ApplicationDbContext db, IHttpContextAccessor http)
{
    private Organization? organization;
    private Employee? employee;
    private bool employeeLoaded;

    public async Task<Organization?> GetAsync(CancellationToken ct = default)
    {
        if (organization is not null) return organization;
        var userId = UserId;
        if (userId is null) return null;

        organization = await db.Organizations
            .Include(o => o.Branches)
            .FirstOrDefaultAsync(o => o.OwnerUserId == userId, ct);
        if (organization is null && await EmployeeAsync(ct) is { } staff)
            organization = await db.Organizations.Include(o => o.Branches).FirstOrDefaultAsync(o => o.UniqueId == staff.OrganizationId, ct);
        return organization;
    }

    /// <summary>The employee record linked to the signed-in login, if any (the owner usually has none).</summary>
    public async Task<Employee?> EmployeeAsync(CancellationToken ct = default)
    {
        if (employeeLoaded) return employee;
        employeeLoaded = true;
        var userId = UserId;
        return employee = userId is null ? null
            : await db.Employees.Include(e => e.Shift).FirstOrDefaultAsync(e => e.UserId == userId, ct);
    }

    private string? UserId => http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>Head office: new employees are added here until the customer sets up more branches.</summary>
    public static Branch DefaultBranch(Organization organization) => organization.Branches.OrderBy(b => b.UniqueId).First();

    /// <summary>The first onboarding step the owner has not finished.</summary>
    public static OnboardingStep NextStep(Organization organization, ClaimsPrincipal user) =>
        user.HasClaim(c => c.Type == TrialService.MustChangePasswordClaim) ? OnboardingStep.Password
        : string.IsNullOrWhiteSpace(organization.Address.Line1) ? OnboardingStep.Profile
        : organization.OperatesInShifts is null ? OnboardingStep.Shifts
        : organization.OnboardingCompletedAt is null ? OnboardingStep.Employees
        : OnboardingStep.Done;
}
