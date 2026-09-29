using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using EMS.Data;
using EMS.Models;
using EMS.Models.Admin;
using EMS.Models.Common;
using EMS.Models.Landing;
using EMS.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.Services.Onboarding;

public record TrialRow(int Id, string Organization, string OwnerEmail, Industry? Industry, DateTime OfferedAt, DateOnly? TrialEndsOn, OnboardingStep Step, int Employees);

public record TrialOffer(Organization Organization, string OwnerEmail, string TemporaryPassword, string EmailSubject, string EmailHtml, bool EmailSent);

/// <summary>
/// Offers a free trial: creates the customer's organization (with a Head office branch) and the owner's login,
/// then emails the owner a temporary password. The owner must change it on first sign-in.
/// </summary>
public class TrialService(
    ApplicationDbContext db,
    UserManager<IdentityUser> users,
    IEmailSender emailSender,
    IOptions<CompanyOptions> company,
    ILogger<TrialService> logger)
{
    /// <summary>User claim present until the user replaces the temporary password.</summary>
    public const string MustChangePasswordClaim = "ems:must_change_password";

    public async Task<ServiceResult<TrialOffer>> OfferAsync(TrialInput input, string loginUrl, CancellationToken ct = default)
    {
        var ownerEmail = input.OwnerEmail.Trim();
        if (await users.FindByEmailAsync(ownerEmail) is not null)
            return ServiceResult<TrialOffer>.Failure($"{ownerEmail} already has an EMS login. Use another email for the owner.");

        var password = TemporaryPassword();
        var owner = new IdentityUser { UserName = ownerEmail, Email = ownerEmail, EmailConfirmed = true };
        var organization = new Organization
        {
            Name = input.OrganizationName.Trim(),
            Email = ownerEmail,
            Phone = input.OwnerPhone.Trim(),
            Industry = input.Industry,
            Owner = owner,
            TrialEndsOn = DateOnly.FromDateTime(DateTime.Today).AddDays(input.TrialDays),
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

        var subject = $"Your {company.Value.ProductName} free trial is ready — sign in to set up {organization.Name}";
        var html = WelcomeEmail(input.OwnerName.Trim(), organization, ownerEmail, password, loginUrl);
        var sent = true;
        try
        {
            await emailSender.SendEmailAsync(ownerEmail, subject, html);
        }
        catch (Exception ex)
        {
            sent = false;
            logger.LogError(ex, "Could not send the trial welcome email to {Email}.", ownerEmail);
        }

        return ServiceResult<TrialOffer>.Success(new TrialOffer(organization, ownerEmail, password, subject, html, sent));
    }

    /// <summary>Every customer organization with an owner login, newest first, with how far onboarding has got.</summary>
    public async Task<List<TrialRow>> TrialsAsync(CancellationToken ct = default)
    {
        var invited = db.UserClaims.Where(c => c.ClaimType == MustChangePasswordClaim).Select(c => c.UserId);
        var rows = await db.Organizations
            .Where(o => o.OwnerUserId != null)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.UniqueId, o.Name, OwnerEmail = o.Owner!.Email, o.Industry, o.CreatedAt, o.TrialEndsOn,
                Invited = invited.Contains(o.OwnerUserId!), HasProfile = o.Address.Line1 != "",
                o.OperatesInShifts, o.OnboardingCompletedAt, Employees = o.Employees.Count(),
            })
            .ToListAsync(ct);

        return rows.Select(o => new TrialRow(o.UniqueId, o.Name, o.OwnerEmail ?? "", o.Industry, o.CreatedAt, o.TrialEndsOn,
            o.Invited ? OnboardingStep.Password
            : !o.HasProfile ? OnboardingStep.Profile
            : o.OperatesInShifts is null ? OnboardingStep.Shifts
            : o.OnboardingCompletedAt is null ? OnboardingStep.Employees
            : OnboardingStep.Done,
            o.Employees)).ToList();
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

    /// <summary>12 characters with upper, lower, digit and symbol, e.g. "Kmt@4821Pqw7"; avoids look-alike characters.</summary>
    private static string TemporaryPassword()
    {
        static string Pick(string chars, int count) =>
            new(Enumerable.Range(0, count).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnpqrstuvwxyz", digits = "23456789";
        return Pick(upper, 1) + Pick(lower, 2) + "@" + Pick(digits, 4) + Pick(upper, 1) + Pick(lower, 2) + Pick(digits, 1);
    }

    private string WelcomeEmail(string ownerName, Organization organization, string email, string password, string loginUrl)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
        var product = company.Value.ProductName;
        var from = company.Value.Name;
        return $$"""
            <!DOCTYPE html>
            <html><body style="margin:0;padding:24px;background:#f5f5f7;font-family:-apple-system,'Segoe UI',Roboto,Arial,sans-serif;color:#1d1d1f;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center">
            <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:16px;padding:32px;">
              <tr><td>
                <p style="margin:0 0 4px;font-size:13px;color:#0071e3;font-weight:600;">{{E(product)}} by {{E(from)}}</p>
                <h1 style="margin:0 0 16px;font-size:24px;">Welcome, {{E(ownerName)}}</h1>
                <p style="margin:0 0 16px;line-height:1.5;">Your free trial for <b>{{E(organization.Name)}}</b> is ready. It runs until <b>{{organization.TrialEndsOn!.Value.ToShortDate()}}</b>.</p>
                <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 20px;background:#f5f5f7;border-radius:12px;width:100%;">
                  <tr><td style="padding:16px 20px;font-size:15px;line-height:1.7;">
                    Sign-in email: <b>{{E(email)}}</b><br />
                    Temporary password: <b style="font-family:Consolas,Menlo,monospace;">{{E(password)}}</b>
                  </td></tr>
                </table>
                <p style="margin:0 0 24px;"><a href="{{E(loginUrl)}}" style="display:inline-block;padding:12px 22px;background:#0071e3;color:#ffffff;border-radius:980px;text-decoration:none;font-weight:600;">Sign in to {{E(product)}}</a></p>
                <p style="margin:0 0 8px;font-weight:600;">After you sign in, we'll walk you through:</p>
                <ol style="margin:0 0 20px;padding-left:20px;line-height:1.7;">
                  <li>Choosing your own password (the temporary one stops working)</li>
                  <li>Completing your organization profile</li>
                  <li>Setting up your shifts — one general shift, or several for 24x7 operations</li>
                  <li>Adding your employees</li>
                </ol>
                <p style="margin:0;font-size:13px;color:#6e6e73;line-height:1.5;">Didn't expect this email? You can ignore it. Questions? Just reply and the {{E(from)}} team will help.</p>
              </td></tr>
            </table>
            </td></tr></table>
            </body></html>
            """;
    }
}
