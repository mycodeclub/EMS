using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using EMS.Areas.Org.Models;
using EMS.Data;
using EMS.Models;
using EMS.Models.Common;
using EMS.Services.Demo;
using EMS.Services.Onboarding;
using EMS.Services.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EMS.Areas.Org.Controllers;

/// <summary>
/// Employee records and onboarding for HR and the owner: personal and job details, IDs and bank account, login access,
/// joining documents (upload, verify), probation confirmation and salary revisions.
/// </summary>
[Authorize(Roles = AppRoles.PeopleManagers)]
public class EmployeesController(
    OrganizationContext context, ApplicationDbContext db, SetupService setup, UserManager<IdentityUser> users,
    IEmailSender emailSender, OnboardingChecklist checklists, DocumentStore documents, PhotoStore photos,
    ILogger<EmployeesController> logger)
    : OrgController(context)
{
    /// <summary>Roles an employee's login can have. Only the owner can grant HR and Accounts.</summary>
    public static readonly (string Role, string Label)[] StaffRoles =
        [(AppRoles.Employee, "Employee"), (AppRoles.OrgHR, "HR"), (AppRoles.OrgAccounts, "Accounts")];

    /// <summary>Everyone on the rolls; <paramref name="onboarding"/> = "pending" shows only those with checklist items left.</summary>
    public async Task<IActionResult> Index(string? q, string? onboarding, CancellationToken ct)
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

        var employees = await query.OrderBy(e => e.EmpCode).ToListAsync(ct);
        var lists = await checklists.ForAsync(employees, ct);
        if (onboarding == "pending")
            employees = employees.Where(e => !lists[e.UniqueId].IsComplete && e.DateOfLeaving is null).ToList();

        ViewData["Query"] = q;
        ViewData["Onboarding"] = onboarding;
        ViewData["Checklists"] = lists;
        return View(employees);
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadEditAsync(null, ct);
        return View("Edit", new EmployeeInput { DateOfJoining = DateOnly.FromDateTime(DateTime.Today), Status = EmployeeStatus.OnProbation,
            ProbationEndsOn = DateOnly.FromDateTime(DateTime.Today).AddMonths(6).AddDays(-1) });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { } employee) return NotFound();
        await LoadEditAsync(employee, ct);
        return View(EmployeeInput.From(employee));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(EmployeeInput input, CancellationToken ct)
    {
        Employee? employee = null;
        if (input.Id is { } id && (employee = await FindAsync(id, ct)) is null) return NotFound();
        await ValidateAsync(input, employee, ct);

        if (employee is null)
        {
            var codes = await setup.EmployeeCodesAsync(Organization.UniqueId, ct);
            input.EmpCode = string.IsNullOrWhiteSpace(input.EmpCode) ? SetupService.NextCode(codes) : input.EmpCode.Trim().ToUpperInvariant();
            if (codes.Contains(input.EmpCode))
                ModelState.AddModelError(nameof(input.EmpCode), $"{input.EmpCode} is already used. Employee codes are never reused.");
            if (input.CreateLogin)
            {
                if (string.IsNullOrWhiteSpace(input.Email)) ModelState.AddModelError(nameof(input.Email), "Enter an email to create the login with.");
                if (!CanGrant(input.LoginRole)) ModelState.AddModelError(nameof(input.LoginRole), "Only the owner can give HR or Accounts access.");
            }
        }

        if (!ModelState.IsValid) return await EditView(input, employee, ct);

        var isNew = employee is null;
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
            return await EditView(input, isNew ? null : employee, ct);
        }

        var message = isNew
            ? $"{employee.FullName} ({employee.EmpCode}) added. Next, collect the joining documents below."
            : $"{employee.FullName} ({employee.EmpCode}) saved.";
        if (isNew && input.CreateLogin)
        {
            var (error, loginMessage) = await GrantLoginAsync(employee, input.Email, input.LoginRole);
            if (error is not null) TempData["Error"] = $"The employee was added, but the login was not created: {error}";
            else message += " " + loginMessage;
        }
        TempData["Message"] = message;
        return RedirectToAction(nameof(Edit), new { id = employee.UniqueId });
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
            TempData["Error"] = $"{employee.FullName} has attendance, leave or documents on record, so they cannot be removed. Set a leaving date instead.";
        }
        return RedirectToAction(nameof(Index));
    }

    // ---------- Probation ----------

    /// <summary>Ends probation: the employee becomes Active, and a confirmation letter becomes available.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { } employee) return NotFound();
        if (employee.Status != EmployeeStatus.OnProbation) return Back(id, $"{employee.FullName} is not on probation.");
        employee.Status = EmployeeStatus.Active;
        employee.ConfirmedOn = DateOnly.FromDateTime(DateTime.Today);
        await db.SaveChangesAsync(ct);
        TempData["Message"] = $"{employee.FullName} is confirmed. The confirmation letter is under Letters.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // ---------- Joining documents ----------

    /// <summary>HR uploads a document they have checked against the original, so it is verified straight away.</summary>
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(DocumentStore.Limit + 64 * 1024)]
    public async Task<IActionResult> UploadDocument(int id, DocumentType type, IFormFile? file, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { } employee) return NotFound();
        if (!Enum.IsDefined(type)) return Back(id, "Choose the document type.", "documents");
        if (file is null) return Back(id, "Choose a file to upload.", "documents");

        var (name, contentType, error) = await documents.SaveAsync(employee.UniqueId, file, ct);
        if (error is not null) return Back(id, error, "documents");
        db.EmployeeDocuments.Add(new EmployeeDocument
        {
            EmployeeId = employee.UniqueId, Type = type, FileName = name!, OriginalName = Path.GetFileName(file.FileName),
            ContentType = contentType!, SizeBytes = file.Length, Status = DocumentStatus.Verified, ReviewedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        TempData["Message"] = $"{type.DisplayName()} uploaded and marked verified.";
        return RedirectToAction(nameof(Edit), null, new { id }, "documents");
    }

    /// <summary>Verifies or rejects a document the employee uploaded. A rejection note tells them what to fix.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewDocument(int id, int documentId, bool verify, string? note, string? returnTo, CancellationToken ct)
    {
        if (await FindDocumentAsync(id, documentId, ct) is not { } document) return NotFound();
        if (!verify && string.IsNullOrWhiteSpace(note))
        {
            TempData["Error"] = "Say why the document is rejected, so the employee knows what to upload instead.";
        }
        else
        {
            document.Status = verify ? DocumentStatus.Verified : DocumentStatus.Rejected;
            document.ReviewedAt = DateTime.UtcNow;
            document.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            await db.SaveChangesAsync(ct);
            TempData["Message"] = $"{document.Type.DisplayName()} {(verify ? "verified" : "rejected")}.";
        }
        return returnTo == "requests" ? RedirectToAction("Index", "Requests") : RedirectToAction(nameof(Edit), null, new { id }, "documents");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(int id, int documentId, CancellationToken ct)
    {
        if (await FindDocumentAsync(id, documentId, ct) is not { } document) return NotFound();
        db.EmployeeDocuments.Remove(document); // soft delete; the file is kept with the record
        await db.SaveChangesAsync(ct);
        TempData["Message"] = $"{document.Type.DisplayName()} removed.";
        return RedirectToAction(nameof(Edit), null, new { id }, "documents");
    }

    /// <summary>Opens a document in the browser (PDF and images display inline).</summary>
    public async Task<IActionResult> Document(int id, int documentId, CancellationToken ct) =>
        await FindDocumentAsync(id, documentId, ct) is { } document && documents.PathOf(document.FileName) is { } path
            ? PhysicalFile(path, document.ContentType)
            : NotFound();

    public async Task<IActionResult> Photo(int id, CancellationToken ct) =>
        await FindAsync(id, ct) is { } employee && photos.PathOf(employee.PhotoPath) is { } path
            ? PhysicalFile(path, UploadStore.ContentType(path))
            : NotFound();

    // ---------- Login access ----------

    /// <summary>
    /// Gives the employee a login with a temporary password (shown once and emailed), as Employee, HR or Accounts.
    /// In the demo organization the email must be @greenfield.test and nothing is emailed.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantAccess(int id, string? email, string? role, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { } employee) return NotFound();
        var (error, message) = await GrantLoginAsync(employee, email, role);
        if (error is not null) return Back(id, error, "login");
        TempData["Message"] = message;
        return RedirectToAction(nameof(Edit), null, new { id }, "login");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAccess(int id, string? role, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { UserId: { } userId } employee || await users.FindByIdAsync(userId) is not { } user) return NotFound();
        if (!CanGrant(role) || !CanGrant((await users.GetRolesAsync(user)).FirstOrDefault()))
            return Back(id, "Only the owner can give or take away HR or Accounts access.", "login");

        await users.RemoveFromRolesAsync(user, StaffRoles.Select(r => r.Role).Where(r => r != role));
        if (!await users.IsInRoleAsync(user, role!)) await users.AddToRoleAsync(user, role!);
        await users.UpdateSecurityStampAsync(user); // signs them out so the new menu applies
        TempData["Message"] = $"{employee.FullName} now signs in as {RoleLabel(role!)}.";
        return RedirectToAction(nameof(Edit), null, new { id }, "login");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeAccess(int id, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { UserId: { } userId } employee) return NotFound();
        if (userId == User.FindFirstValue(ClaimTypes.NameIdentifier)) return Back(id, "You cannot remove your own login.", "login");
        var user = await users.FindByIdAsync(userId);
        if (user is not null && !CanGrant((await users.GetRolesAsync(user)).FirstOrDefault()))
            return Back(id, "Only the owner can remove an HR or Accounts login.", "login");

        employee.UserId = null;
        await db.SaveChangesAsync(ct);
        if (user is not null) await users.DeleteAsync(user);
        TempData["Message"] = $"{employee.FullName}'s login was removed.";
        return RedirectToAction(nameof(Edit), null, new { id }, "login");
    }

    // ---------- Salary ----------

    /// <summary>Records an appraisal: the new monthly salary applies from now on, and an appraisal letter becomes available.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revise(int id, [Bind(Prefix = "Revision")] SalaryRevisionInput input, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { } employee) return NotFound();
        if (!ModelState.IsValid) return Back(id, string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
        if (input.EffectiveFrom < employee.DateOfJoining) return Back(id, "A revision cannot take effect before the joining date.");

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

    // ---------- Helpers ----------

    /// <summary>Dates that make sense, a manager from this organization, and no other employee with the same contact or IDs.</summary>
    private async Task ValidateAsync(EmployeeInput input, Employee? employee, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (input.DateOfLeaving is { } left && input.DateOfJoining is { } joined && left < joined)
            ModelState.AddModelError(nameof(input.DateOfLeaving), "The leaving date cannot be before the joining date.");
        if (input.ProbationEndsOn is { } probation && input.DateOfJoining is { } start && probation < start)
            ModelState.AddModelError(nameof(input.ProbationEndsOn), "Probation cannot end before the joining date.");
        if (input.DateOfBirth is { } born)
        {
            if (born > today) ModelState.AddModelError(nameof(input.DateOfBirth), "The date of birth is in the future.");
            else if (input.DateOfJoining is { } joining && born.AddYears(14) > joining)
                ModelState.AddModelError(nameof(input.DateOfBirth), "The employee must be at least 14 on the joining date.");
        }
        if (input.ShiftId is { } shiftId && !await db.Shifts.AnyAsync(s => s.UniqueId == shiftId && s.OrganizationId == Organization.UniqueId, ct))
            ModelState.AddModelError(nameof(input.ShiftId), "Choose one of your shifts.");
        if (input.ReportingManagerId is { } managerId
            && (managerId == employee?.UniqueId || !await db.Employees.AnyAsync(e => e.UniqueId == managerId && e.OrganizationId == Organization.UniqueId, ct)))
            ModelState.AddModelError(nameof(input.ReportingManagerId), "Choose another employee of your organization as the manager.");

        var others = db.Employees.Where(e => e.OrganizationId == Organization.UniqueId && e.UniqueId != (employee == null ? 0 : employee.UniqueId));
        async Task Unique(string field, string? value, System.Linq.Expressions.Expression<Func<Employee, bool>> match, string label)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (await others.Where(match).Select(e => e.FirstName + " " + (e.LastName ?? "") + " (" + e.EmpCode + ")").FirstOrDefaultAsync(ct) is { } who)
                ModelState.AddModelError(field, $"This {label} is already on {who.Replace("  ", " ")}'s record.");
        }
        var email = input.Email?.Trim().ToLowerInvariant();
        var mobile = input.Mobile?.Trim();
        var pan = input.Pan?.Trim().ToUpperInvariant();
        var aadhaar = input.Aadhaar?.Trim();
        await Unique(nameof(input.Email), email, e => e.Email != null && e.Email.ToLower() == email, "email");
        await Unique(nameof(input.Mobile), mobile, e => e.Mobile == mobile, "mobile number");
        await Unique(nameof(input.Pan), pan, e => e.Pan == pan, "PAN");
        await Unique(nameof(input.Aadhaar), aadhaar, e => e.Aadhaar == aadhaar, "Aadhaar number");
    }

    private async Task<(string? Error, string? Message)> GrantLoginAsync(Employee employee, string? email, string? role)
    {
        email = email?.Trim();
        var error = employee.UserId is not null ? $"{employee.FullName} already has a login."
            : string.IsNullOrEmpty(email) || !new EmailAddressAttribute().IsValid(email) ? "Enter a valid email for the login."
            : !CanGrant(role) ? "Only the owner can give HR or Accounts access."
            : Organization.IsDemo && !email.EndsWith("@greenfield.test", StringComparison.OrdinalIgnoreCase) ? "In the demo, use an email ending in @greenfield.test."
            : await users.FindByEmailAsync(email) is not null ? $"{email} already has an EMS login."
            : null;
        if (error is not null) return (error, null);

        var password = TrialService.TemporaryPassword();
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        if (result.Succeeded) result = await users.AddToRoleAsync(user, role!);
        if (result.Succeeded && Organization.IsDemo) result = await users.AddClaimAsync(user, new Claim(DemoSeeder.DemoClaim, "true"));
        if (!result.Succeeded)
        {
            if (await users.FindByIdAsync(user.Id) is { } created) await users.DeleteAsync(created);
            return (string.Join(" ", result.Errors.Select(e => e.Description)), null);
        }

        employee.UserId = user.Id;
        employee.Email ??= email;
        await db.SaveChangesAsync();
        logger.LogInformation("Login {Email} ({Role}) created for employee {Code} of organization {Org}.", email, role, employee.EmpCode, Organization.UniqueId);

        if (!Organization.IsDemo)
        {
            try
            {
                var link = Url.Page("/Account/Login", null, new { area = "Identity" }, Request.Scheme);
                await emailSender.SendEmailAsync(email!, $"Your {Organization.Name} login for EMS",
                    $"<p>Hello {WebUtility.HtmlEncode(employee.FirstName)},</p><p>Welcome to {WebUtility.HtmlEncode(Organization.Name)}! You can now sign in to EMS.</p>"
                    + $"<p>Email: <b>{WebUtility.HtmlEncode(email)}</b><br />Temporary password: <b>{WebUtility.HtmlEncode(password)}</b></p>"
                    + $"<p><a href=\"{link}\">Sign in</a>, change your password under Account &amp; password, then complete your <b>Joining checklist</b>: "
                    + "your details, PAN, Aadhaar, bank account, photo and joining documents.</p>");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not email the new login to {Email}.", email);
            }
        }

        return (null, $"Login created as {RoleLabel(role!)}: email {email}, temporary password {password} (shown once"
            + (Organization.IsDemo ? ")." : "; also emailed)."));
    }

    private bool CanGrant(string? role) =>
        role == AppRoles.Employee || (User.IsInRole(AppRoles.OrgAdmin) && StaffRoles.Any(r => r.Role == role));

    private static string RoleLabel(string role) => StaffRoles.FirstOrDefault(r => r.Role == role).Label ?? role;

    private RedirectToActionResult Back(int id, string error, string? fragment = null)
    {
        TempData["Error"] = error;
        return RedirectToAction(nameof(Edit), null, new { id }, fragment);
    }

    private Task<Employee?> FindAsync(int id, CancellationToken ct) =>
        db.Employees.FirstOrDefaultAsync(e => e.UniqueId == id && e.OrganizationId == Organization.UniqueId, ct);

    private Task<EmployeeDocument?> FindDocumentAsync(int employeeId, int documentId, CancellationToken ct) =>
        db.EmployeeDocuments.FirstOrDefaultAsync(d => d.UniqueId == documentId && d.EmployeeId == employeeId && d.Employee.OrganizationId == Organization.UniqueId, ct);

    private async Task<IActionResult> EditView(EmployeeInput input, Employee? employee, CancellationToken ct)
    {
        await LoadEditAsync(employee, ct);
        return View("Edit", input);
    }

    /// <summary>Options for the form, and for an existing employee the checklist, documents, login, revisions and saved IDs.</summary>
    private async Task LoadEditAsync(Employee? employee, CancellationToken ct)
    {
        var shifts = await setup.ShiftsAsync(Organization.UniqueId, ct);
        ViewData["Shifts"] = shifts.Select(s => new SelectListItem($"{s.Name} ({s.StartTime:HH\\:mm}–{s.EndTime:HH\\:mm})", s.UniqueId.ToString())).ToList();
        ViewData["Managers"] = (await db.Employees
                .Where(e => e.OrganizationId == Organization.UniqueId && e.DateOfLeaving == null && e.UniqueId != (employee == null ? 0 : employee.UniqueId))
                .OrderBy(e => e.FirstName).Select(e => new { e.UniqueId, e.FirstName, e.LastName, e.Designation }).ToListAsync(ct))
            .Select(e => new SelectListItem($"{e.FirstName} {e.LastName}{(e.Designation is null ? "" : $" · {e.Designation}")}", e.UniqueId.ToString())).ToList();
        ViewData["NextCode"] = SetupService.NextCode(await setup.EmployeeCodesAsync(Organization.UniqueId, ct));
        ViewData["CanGrantManagers"] = User.IsInRole(AppRoles.OrgAdmin);
        if (employee is null) return;

        ViewData["Employee"] = employee;
        ViewData["Revisions"] = await db.SalaryRevisions.Where(r => r.EmployeeId == employee.UniqueId).OrderByDescending(r => r.EffectiveFrom).ToListAsync(ct);
        ViewData["Documents"] = await db.EmployeeDocuments.Where(d => d.EmployeeId == employee.UniqueId).OrderByDescending(d => d.UniqueId).ToListAsync(ct);
        ViewData["Checklist"] = await checklists.ForAsync(employee, ct);
        if (employee.UserId is { } userId && await users.FindByIdAsync(userId) is { } user)
            ViewData["Login"] = (user.Email, (await users.GetRolesAsync(user)).FirstOrDefault());
    }
}
