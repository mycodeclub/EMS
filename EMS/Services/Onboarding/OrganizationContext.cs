using System.Security.Claims;
using EMS.Data;
using EMS.Models;
using Microsoft.EntityFrameworkCore;

namespace EMS.Services.Onboarding;

public enum OnboardingStep { Password = 1, Profile, Shifts, Employees, Done }

/// <summary>The organization owned by the signed-in OrgAdmin (loaded once per request).</summary>
public class OrganizationContext(ApplicationDbContext db, IHttpContextAccessor http)
{
    private Organization? organization;

    public async Task<Organization?> GetAsync(CancellationToken ct = default)
    {
        if (organization is not null) return organization;
        var userId = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return null;

        return organization = await db.Organizations
            .Include(o => o.Branches)
            .FirstOrDefaultAsync(o => o.OwnerUserId == userId, ct);
    }

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
