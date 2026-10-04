using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Identity;
using EMS.Services.Demo;
using System.Security.Claims;
using System.Net;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using EMS.Models.Common;
using EMS.Areas.Org.Models;
using EMS.Data;
using EMS.Models;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EMS.Areas.Org.Controllers;

[Authorize(Roles = AppRoles.PeopleManagers)]
public class EmployeesController(
    OrganizationContext context, ApplicationDbContext db, SetupService setup, UserManager<IdentityUser> users,
    IEmailSender emailSender, ILogger<EmployeesController> logger)
    : OrgController(context)
{
    /// <summary>Roles an employee's login can have. Only the owner can grant HR and Accounts.</summary>
    public static readonly (string Role, string Label)[] StaffRoles =
        [(AppRoles.Employee, "Employee"), (AppRoles.OrgHR, "HR"), (AppRoles.OrgAccounts, "Accounts")];

    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        var query = db.Employees.Include(e => e.Shift).Where(e => e.OrganizationId == Organization.UniqueId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.FirstName + " " + (e.LastName ?? ""), term)
                                     || EF.Functions.ILike(e.EmpCode, term)
                                     || (e.Designation != null && EF.Functions.ILike(e.Designation, term))
                                     || (e.Mobile != null && EF.Functions.ILike(e.Mobile, term)));
        }

        ViewData["Query"] = q;
        return View(await query.OrderBy(e => e.EmpCode).ToListAsync(ct));
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadOptionsAsync(ct);
        return View("Edit", new EmployeeInput { DateOfJoining = DateOnly.FromDateTime(DateTime.Today) });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { } employee) return NotFound();
        await LoadOptionsAsync(ct);
        await LoadRevisionsAsync(id, ct);
        await LoadLoginAsync(employee);
        return View(EmployeeInput.From(employee));
    }

    /// <summary>
    /// Gives the employee a login with a temporary password (shown once and emailed), as Employee, HR or Accounts.
    /// In the demo organization the email must be @greenfield.test and nothing is emailed.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantAccess(int id, string? email, string? role, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { } employee) return NotFound();
        email = email?.Trim();
        var error = employee.UserId is not null ? $"{employee.FullName} already has a login."
            : string.IsNullOrEmpty(email) || !new EmailAddressAttribute().IsValid(email) ? "Enter a valid email for the login."
            : !CanGrant(role) ? "Only the owner can give HR or Accounts access."
            : Organization.IsDemo && !email.EndsWith("@greenfield.test", StringComparison.OrdinalIgnoreCase) ? "In the demo, use an email ending in @greenfield.test."
            : await users.FindByEmailAsync(email) is not null ? $"{email} already has an EMS login."
            : null;
        if (error is not null) return Back(id, error);

        var password = TrialService.TemporaryPassword();
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        if (result.Succeeded) result = await users.AddToRoleAsync(user, role!);
        if (result.Succeeded && Organization.IsDemo) result = await users.AddClaimAsync(user, new System.Security.Claims.Claim(DemoSeeder.DemoClaim, "true"));
        if (!result.Succeeded)
        {
            if (user.Id is not null && await users.FindByIdAsync(user.Id) is { } created) await users.DeleteAsync(created);
            return Back(id, string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        employee.UserId = user.Id;
        employee.Email ??= email;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Login {Email} ({Role}) created for employee {Code} of organization {Org}.", email, role, employee.EmpCode, Organization.UniqueId);

        if (!Organization.IsDemo)
        {
            try
            {
                var link = Url.Page("/Account/Login", null, new { area = "Identity" }, Request.Scheme);
                await emailSender.SendEmailAsync(email, $"Your {Organization.Name} login for EMS",
                    $"<p>Hello {WebUtility.HtmlEncode(employee.FirstName)},</p><p>You can now sign in to EMS for {WebUtility.HtmlEncode(Organization.Name)}.</p>"
                    + $"<p>Email: <b>{WebUtility.HtmlEncode(email)}</b><br />Temporary password: <b>{WebUtility.HtmlEncode(password)}</b></p>"
                    + $"<p><a href=\"{link}\">Sign in</a>, then change your password under Account &amp; password.</p>");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not email the new login to {Email}.", email);
            }
        }

        TempData["Message"] = $"Login created for {employee.FullName} as {RoleLabel(role!)}. Email {email}, temporary password {password} (shown once"
            + (Organization.IsDemo ? ")." : "; also emailed).");
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAccess(int id, string? role, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { UserId: { } userId } employee || await users.FindByIdAsync(userId) is not { } user) return NotFound();
        if (!CanGrant(role) || !CanGrant((await users.GetRolesAsync(user)).FirstOrDefault()))
            return Back(id, "Only the owner can give or take away HR or Accounts access.");

        await users.RemoveFromRolesAsync(user, StaffRoles.Select(r => r.Role).Where(r => r != role));
        if (!await users.IsInRoleAsync(user, role!)) await users.AddToRoleAsync(user, role!);
        await users.UpdateSecurityStampAsync(user); // signs them out so the new menu applies
        TempData["Message"] = $"{employee.FullName} now signs in as {RoleLabel(role!)}.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeAccess(int id, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { UserId: { } userId } employee) return NotFound();
        if (userId == User.FindFirstValue(ClaimTypes.NameIdentifier)) return Back(id, "You cannot remove your own login.");
        var user = await users.FindByIdAsync(userId);
        if (user is not null && !CanGrant((await users.GetRolesAsync(user)).FirstOrDefault()))
            return Back(id, "Only the owner can remove an HR or Accounts login.");

        employee.UserId = null;
        await db.SaveChangesAsync(ct);
        if (user is not null) await users.DeleteAsync(user);
        TempData["Message"] = $"{employee.FullName}'s login was removed.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private bool CanGrant(string? role) =>
        role == AppRoles.Employee || (User.IsInRole(AppRoles.OrgAdmin) && StaffRoles.Any(r => r.Role == role));

    private static string RoleLabel(string role) => StaffRoles.FirstOrDefault(r => r.Role == role).Label ?? role;

    private RedirectToActionResult Back(int id, string error)
    {
        TempData["Error"] = error;
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task LoadLoginAsync(Employee employee)
    {
        if (employee.UserId is { } userId && await users.FindByIdAsync(userId) is { } user)
            ViewData["Login"] = (user.Email, (await users.GetRolesAsync(user)).FirstOrDefault());
        ViewData["CanGrantManagers"] = User.IsInRole(AppRoles.OrgAdmin);
    }

    /// <summary>Records an appraisal: the new monthly salary applies from now on, and an appraisal letter becomes available.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revise(int id, [Bind(Prefix = "Revision")] SalaryRevisionInput input, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { } employee) return NotFound();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Edit), new { id });
        }
        if (input.EffectiveFrom < employee.DateOfJoining)
        {
            TempData["Error"] = "A revision cannot take effect before the joining date.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        db.SalaryRevisions.Add(new SalaryRevision
        {
            EmployeeId = id, EffectiveFrom = input.EffectiveFrom!.Value, PreviousSalary = employee.MonthlySalary ?? 0,
            NewSalary = input.NewSalary!.Value, Remarks = input.Remarks?.Trim(),
        });
        employee.MonthlySalary = input.NewSalary;
        await db.SaveChangesAsync(ct);
        TempData["Message"] = $"Salary revised to {input.NewSalary!.Value.ToRupees()} a month. The appraisal letter is ready under Letters.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(EmployeeInput input, CancellationToken ct)
    {
        if (input.DateOfLeaving is { } left && input.DateOfJoining is { } joined && left < joined)
            ModelState.AddModelError(nameof(input.DateOfLeaving), "The leaving date cannot be before the joining date.");
        if (input.ShiftId is { } shiftId && !await db.Shifts.AnyAsync(s => s.UniqueId == shiftId && s.OrganizationId == Organization.UniqueId, ct))
            ModelState.AddModelError(nameof(input.ShiftId), "Choose one of your shifts.");

        Employee? employee = null;
        if (input.Id is { } id && (employee = await FindAsync(id, ct)) is null) return NotFound();

        if (employee is null)
        {
            var codes = await setup.EmployeeCodesAsync(Organization.UniqueId, ct);
            input.EmpCode = string.IsNullOrWhiteSpace(input.EmpCode) ? SetupService.NextCode(codes) : input.EmpCode.Trim().ToUpperInvariant();
            if (codes.Contains(input.EmpCode))
                ModelState.AddModelError(nameof(input.EmpCode), $"{input.EmpCode} is already used. Employee codes are never reused.");
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(ct);
            if (input.Id is { } existingId) await LoadRevisionsAsync(existingId, ct);
            return View("Edit", input);
        }

        if (employee is null)
        {
            employee = new Employee
            {
                OrganizationId = Organization.UniqueId,
                BranchId = OrganizationContext.DefaultBranch(Organization).UniqueId,
                EmpCode = input.EmpCode!,
            };
            db.Employees.Add(employee);
        }
        input.ApplyTo(employee);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (BusinessRuleException ex)
        {
            logger.LogInformation("Employee save refused: {Reason}", ex.Message);
            db.ChangeTracker.Clear();
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadOptionsAsync(ct);
            if (input.Id is { } existingId) await LoadRevisionsAsync(existingId, ct);
            return View("Edit", input);
        }

        TempData["Message"] = $"{employee.FullName} ({employee.EmpCode}) saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { } employee) return NotFound();
        db.Employees.Remove(employee); // soft delete
        try
        {
            await db.SaveChangesAsync(ct);
            TempData["Message"] = $"{employee.FullName} removed.";
        }
        catch (BusinessRuleException)
        {
            db.ChangeTracker.Clear();
            TempData["Error"] = $"{employee.FullName} has attendance or leave records, so they cannot be removed. Set a leaving date instead.";
        }
        return RedirectToAction(nameof(Index));
    }

    private Task<Employee?> FindAsync(int id, CancellationToken ct) =>
        db.Employees.FirstOrDefaultAsync(e => e.UniqueId == id && e.OrganizationId == Organization.UniqueId, ct);

    private async Task LoadRevisionsAsync(int employeeId, CancellationToken ct) =>
        ViewData["Revisions"] = await db.SalaryRevisions.Where(r => r.EmployeeId == employeeId).OrderByDescending(r => r.EffectiveFrom).ToListAsync(ct);

    private async Task LoadOptionsAsync(CancellationToken ct)
    {
        var shifts = await setup.ShiftsAsync(Organization.UniqueId, ct);
        ViewData["Shifts"] = shifts.Select(s => new SelectListItem($"{s.Name} ({s.StartTime:HH\\:mm}–{s.EndTime:HH\\:mm})", s.UniqueId.ToString())).ToList();
        ViewData["NextCode"] = SetupService.NextCode(await setup.EmployeeCodesAsync(Organization.UniqueId, ct));
    }
}
