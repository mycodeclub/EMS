using System.Security.Claims;
using EMS.Data;
using EMS.Models;
using EMS.Models.Admin;
using EMS.Models.Common;
using EMS.Services.Auth;
using EMS.Services.Common;
using EMS.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EMS.Services.Onboarding;

public record TrialRow(int Id, string Organization, string OwnerEmail, Industry? Industry, DateTime OfferedAt, DateOnly? TrialEndsOn, OnboardingStep Step, int Employees, bool IsDemo = false);

public record TrialOffer(Organization Organization, string OwnerEmail, string TemporaryPassword, string EmailSubject, string EmailHtml, bool EmailSent);

/// <summary>
/// Offers a free trial: creates the customer's organization (with a Head office branch) and the owner's login,
/// then emails the owner a temporary password. The owner must change it on first sign-in.
/// </summary>
public class TrialService(
    ApplicationDbContext db,
    UserManager<IdentityUser> users,
    IEmailService email,
    EmailTemplates templates,
    ILogger<TrialService> logger)
{
    /// <summary>User claim present until the user replaces the temporary password.</summary>
    public const string MustChangePasswordClaim = "ems:must_change_password";

    public async Task<ServiceResult<TrialOffer>> OfferAsync(TrialInput input, string loginUrl, CancellationToken ct = default)
    {
        var ownerEmail = input.OwnerEmail.Trim();
        if (await users.FindByEmailAsync(ownerEmail) is not null)
            return ServiceResult<TrialOffer>.Failure($"{ownerEmail} already has an EMS login. Use another email for the owner.");

        var password = TemporaryPassword.Create();
        var owner = new IdentityUser { UserName = ownerEmail, Email = ownerEmail, EmailConfirmed = true };
        var organization = new Organization
        {
            Name = input.OrganizationName.Trim(),
            Email = ownerEmail,
            Phone = input.OwnerPhone.Trim(),
            Industry = input.Industry,
            Owner = owner,
            TrialEndsOn = DateOnly.FromDateTime(AppClock.Today).AddDays(input.TrialDays),
            // 24x7 businesses work on rosters, not a holiday list; confirmed during onboarding.
            IsHolidayCalendarApplicable = input.Industry is not (Industry.Hospital or Industry.Hotel),
        };
        organization.Branches.Add(new Branch { Organization = organization, Name = "Head office", Code = "HO" });
        organization.Contacts.Add(new ContactPerson
        {
            Organization = organization,
            Role = ContactRole.Primary,
            Name = input.OwnerName.Trim(),
            Designation = "Owner",
            Phone = input.OwnerPhone.Trim(),
            Email = ownerEmail,
        });

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var errors = await CreateOwnerAsync(owner, password);
            if (errors.Length > 0) return ServiceResult<TrialOffer>.Failure(errors);

            db.Organizations.Add(organization);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        logger.LogInformation("Free trial offered to {Organization} (#{Id}), owner {Email}.", organization.Name, organization.UniqueId, ownerEmail);

        var welcome = templates.TrialWelcome(ownerEmail, input.OwnerName.Trim(), organization, password, loginUrl);
        var sent = await email.TrySendAsync(welcome);

        return ServiceResult<TrialOffer>.Success(new TrialOffer(organization, ownerEmail, password, welcome.Subject, welcome.HtmlBody, sent));
    }

    /// <summary>Every customer organization with an owner login (the demo first, then newest first), with how far onboarding has got.</summary>
    public async Task<List<TrialRow>> TrialsAsync(CancellationToken ct = default)
    {
        var invited = db.UserClaims.Where(c => c.ClaimType == MustChangePasswordClaim).Select(c => c.UserId);
        var rows = await db.Organizations
            .Where(o => o.OwnerUserId != null)
            .OrderByDescending(o => o.IsDemo).ThenByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.UniqueId, o.Name, OwnerEmail = o.Owner!.Email, o.Industry, o.CreatedAt, o.TrialEndsOn,
                Invited = invited.Contains(o.OwnerUserId!), HasProfile = o.Address.Line1 != "",
                o.OperatesInShifts, o.OnboardingCompletedAt, Employees = o.Employees.Count(), o.IsDemo,
            })
            .ToListAsync(ct);

        return rows.Select(o => new TrialRow(o.UniqueId, o.Name, o.OwnerEmail ?? "", o.Industry, o.CreatedAt, o.TrialEndsOn,
            o.Invited ? OnboardingStep.Password
            : !o.HasProfile ? OnboardingStep.Profile
            : o.OperatesInShifts is null ? OnboardingStep.Shifts
            : o.OnboardingCompletedAt is null ? OnboardingStep.Employees
            : OnboardingStep.Done,
            o.Employees, o.IsDemo)).ToList();
    }

    /// <summary>Owner emails of organizations that already have a trial or subscription.</summary>
    public Task<List<string>> OwnerEmailsAsync(CancellationToken ct = default) =>
        db.Organizations.Where(o => o.Owner != null).Select(o => o.Owner!.Email!).ToListAsync(ct);

    private async Task<string[]> CreateOwnerAsync(IdentityUser owner, string password)
    {
        foreach (var step in new Func<Task<IdentityResult>>[]
                 {
                     () => users.CreateAsync(owner, password),
                     () => users.AddToRoleAsync(owner, AppRoles.OrgAdmin),
                     () => users.AddClaimAsync(owner, new Claim(MustChangePasswordClaim, "true")),
                 })
        {
            var result = await step();
            if (!result.Succeeded) return result.Errors.Select(e => e.Description).ToArray();
        }
        return [];
    }
}
