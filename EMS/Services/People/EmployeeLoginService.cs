using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EMS.Data;
using EMS.Models;
using EMS.Services.Auth;
using EMS.Services.Common;
using EMS.Services.Demo;
using EMS.Services.Email;
using Microsoft.AspNetCore.Identity;

namespace EMS.Services.People;

/// <summary>A login created for an employee. The password is shown to HR once; <see cref="Emailed"/> says whether the employee also got it by email.</summary>
public record EmployeeLogin(string Email, string Role, string TemporaryPassword, bool Emailed);

/// <summary>
/// Gives an employee their own login (Employee, HR or Accounts) with a temporary password, and emails them the details.
/// In the demo organization the email must be @greenfield.test, the login is marked as a demo login and nothing is emailed.
/// Who may grant which role is the caller's check.
/// </summary>
public class EmployeeLoginService(
    ApplicationDbContext db, UserManager<IdentityUser> users, IEmailService email, EmailTemplates templates,
    ILogger<EmployeeLoginService> logger)
{
    public const string DemoEmailDomain = "@greenfield.test";

    public async Task<ServiceResult<EmployeeLogin>> GrantAsync(Organization organization, Employee employee, string? emailAddress, string role, string loginUrl)
    {
        var address = emailAddress?.Trim();
        var error = employee.UserId is not null ? $"{employee.FullName} already has a login."
            : string.IsNullOrEmpty(address) || !new EmailAddressAttribute().IsValid(address) ? "Enter a valid email for the login."
            : organization.IsDemo && !address.EndsWith(DemoEmailDomain, StringComparison.OrdinalIgnoreCase) ? $"In the demo, use an email ending in {DemoEmailDomain}."
            : await users.FindByEmailAsync(address) is not null ? $"{address} already has an EMS login."
            : null;
        if (error is not null) return ServiceResult<EmployeeLogin>.Failure(error);

        var password = TemporaryPassword.Create();
        var user = new IdentityUser { UserName = address, Email = address, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        if (result.Succeeded) result = await users.AddToRoleAsync(user, role);
        if (result.Succeeded && organization.IsDemo) result = await users.AddClaimAsync(user, new Claim(DemoSeeder.DemoClaim, "true"));
        if (!result.Succeeded)
        {
            if (await users.FindByIdAsync(user.Id) is { } created) await users.DeleteAsync(created);
            return ServiceResult<EmployeeLogin>.Failure(result.Errors.Select(e => e.Description).ToArray());
        }

        employee.UserId = user.Id;
        employee.Email ??= address;
        await db.SaveChangesAsync();
        logger.LogInformation("Login {Email} ({Role}) created for employee {Code} of organization {Org}.", address, role, employee.EmpCode, organization.UniqueId);

        var emailed = !organization.IsDemo
            && await email.TrySendAsync(templates.EmployeeLogin(address!, employee.FirstName, organization.Name, password, loginUrl));
        return ServiceResult<EmployeeLogin>.Success(new EmployeeLogin(address!, role, password, emailed));
    }
}
