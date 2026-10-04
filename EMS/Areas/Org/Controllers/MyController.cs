using EMS.Areas.Org.Models;
using EMS.Data;
using EMS.Models;
using EMS.Models.Common;
using EMS.Services.Leave;
using EMS.Services.Onboarding;
using EMS.Services.Payroll;
using EMS.Services.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Areas.Org.Controllers;

/// <summary>
/// Self-service for staff with a login: their attendance and shift, salary slips, leave, profile (photo, personal
/// details, previous jobs, PAN / Aadhaar, bank account) and resignation. Letters are in <see cref="LettersController"/>.
/// </summary>
[Authorize(Roles = AppRoles.SelfService)]
public class MyController(
    OrganizationContext context, ApplicationDbContext db, LeaveService leave, ResignationService resignations, PhotoStore photos)
    : OrgController(context)
{
    private readonly OrganizationContext context = context;

    // ---------- Attendance & slips ----------

    public async Task<IActionResult> Index(int? year, int? month, CancellationToken ct)
    {
        var (y, m) = ResolveMonth(year, month);
        if (await context.EmployeeAsync(ct) is not { } employee) return View(new MyMonth(y, m, null, [], null));

        var records = await RecordsAsync(employee.UniqueId, y, m, ct);
        var byDay = records.GroupBy(a => a.AttendanceDate).ToDictionary(g => g.Key, g => g.OrderBy(a => a.UniqueId).First());
        var today = DateOnly.FromDateTime(DateTime.Today);
        var days = PayrollCalculator.EmployedRange(employee, y, m) is { } range
            ? Enumerable.Range(0, range.To.DayNumber - range.From.DayNumber + 1).Select(range.From.AddDays)
                .Where(d => d <= today).Select(d => new MyDay(d, byDay.GetValueOrDefault(d))).ToList()
            : [];
        var pay = days.Count > 0 ? PayrollCalculator.Calculate(employee, y, m, records) : null;
        ViewData["Resignation"] = await resignations.CurrentAsync(employee.UniqueId, ct);
        return View(new MyMonth(y, m, employee, days, pay));
    }

    public async Task<IActionResult> Slip(int? year, int? month, CancellationToken ct)
    {
        var (y, m) = ResolveMonth(year, month);
        if (await context.EmployeeAsync(ct) is not { } employee || PayrollCalculator.EmployedRange(employee, y, m) is null) return NotFound();

        ViewData["Organization"] = Organization;
        ViewData["BackLabel"] = "‹ My attendance";
        return View("~/Areas/Org/Views/Payroll/Slip.cshtml",
            PayrollCalculator.Calculate(employee, y, m, await RecordsAsync(employee.UniqueId, y, m, ct)));
    }

    // ---------- Leave ----------

    public async Task<IActionResult> Leave(CancellationToken ct) =>
        await context.EmployeeAsync(ct) is { } employee ? await LeaveView(employee, new LeaveRequestInput(), ct) : NoEmployee();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(LeaveRequestInput input, CancellationToken ct)
    {
        if (await context.EmployeeAsync(ct) is not { } employee) return NoEmployee();
        if (ModelState.IsValid && await leave.ApplyAsync(employee, input, ct) is { } error) ModelState.AddModelError(string.Empty, error);
        if (!ModelState.IsValid) return await LeaveView(employee, input, ct);

        TempData["Message"] = "Leave applied. HR will approve or reject it; you can follow it below.";
        return RedirectToAction(nameof(Leave));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelLeave(int id, CancellationToken ct)
    {
        if (await context.EmployeeAsync(ct) is not { } employee) return NoEmployee();
        if (await leave.CancelAsync(employee.UniqueId, id, ct)) TempData["Message"] = "Leave application cancelled.";
        else TempData["Error"] = "Only leave that is still waiting for approval can be cancelled.";
        return RedirectToAction(nameof(Leave));
    }

    private async Task<IActionResult> LeaveView(Employee employee, LeaveRequestInput input, CancellationToken ct)
    {
        var year = DateTime.Today.Year;
        return View("Leave", new MyLeavePage(employee, year, await leave.BalancesAsync(employee, year, ct),
            await leave.ApplicationsAsync(employee.UniqueId, ct), input));
    }

    // ---------- Profile ----------

    public async Task<IActionResult> Profile(CancellationToken ct) =>
        await context.EmployeeAsync(ct) is { } employee ? await ProfileView(employee, MyProfileInput.From(employee), ct) : NoEmployee();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(MyProfileInput input, CancellationToken ct)
    {
        if (await context.EmployeeAsync(ct) is not { } employee) return NoEmployee();
        if (input.DateOfBirth is { } born && born > DateOnly.FromDateTime(DateTime.Today).AddYears(-14))
            ModelState.AddModelError(nameof(input.DateOfBirth), "Check the date of birth.");
        if (!ModelState.IsValid) return await ProfileView(employee, input, ct);

        input.ApplyTo(employee);
        await db.SaveChangesAsync(ct);
        TempData["Message"] = "Profile saved.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(PhotoStore.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> UploadPhoto(IFormFile? photo, CancellationToken ct)
    {
        if (await context.EmployeeAsync(ct) is not { } employee) return NoEmployee();
        if (photo is null)
        {
            TempData["Error"] = "Choose a photo.";
            return RedirectToAction(nameof(Profile));
        }

        var (name, error) = await photos.SaveAsync(employee.UniqueId, photo, ct);
        if (error is not null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Profile));
        }
        var old = employee.PhotoPath;
        employee.PhotoPath = name;
        await db.SaveChangesAsync(ct);
        photos.Delete(old);
        TempData["Message"] = "Photo updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePhoto(CancellationToken ct)
    {
        if (await context.EmployeeAsync(ct) is not { } employee) return NoEmployee();
        photos.Delete(employee.PhotoPath);
        employee.PhotoPath = null;
        await db.SaveChangesAsync(ct);
        TempData["Message"] = "Photo removed.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExperience([Bind(Prefix = "NewExperience")] ExperienceInput input, CancellationToken ct)
    {
        if (await context.EmployeeAsync(ct) is not { } employee) return NoEmployee();
        if (input.FromDate is { } from && input.ToDate is { } to)
        {
            if (to < from) ModelState.AddModelError("NewExperience.ToDate", "The job cannot end before it starts.");
            if (to > employee.DateOfJoining) ModelState.AddModelError("NewExperience.ToDate", "Previous jobs end before you joined here.");
        }
        if (!ModelState.IsValid) return await ProfileView(employee, MyProfileInput.From(employee), ct, input);

        db.EmployeeExperiences.Add(new EmployeeExperience
        {
            EmployeeId = employee.UniqueId, Company = input.Company.Trim(), Designation = input.Designation?.Trim(),
            FromDate = input.FromDate!.Value, ToDate = input.ToDate!.Value,
        });
        await db.SaveChangesAsync(ct);
        await UpdatePriorExperienceAsync(employee, ct);
        TempData["Message"] = "Previous job added.";
        return RedirectToAction(nameof(Profile), null, "experience");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveExperience(int id, CancellationToken ct)
    {
        if (await context.EmployeeAsync(ct) is not { } employee) return NoEmployee();
        if (await db.EmployeeExperiences.FirstOrDefaultAsync(x => x.UniqueId == id && x.EmployeeId == employee.UniqueId, ct) is { } job)
        {
            db.EmployeeExperiences.Remove(job); // soft delete
            await db.SaveChangesAsync(ct);
            await UpdatePriorExperienceAsync(employee, ct);
            TempData["Message"] = $"{job.Company} removed.";
        }
        return RedirectToAction(nameof(Profile), null, "experience");
    }

    /// <summary>The signed-in employee's photo.</summary>
    public async Task<IActionResult> Photo(CancellationToken ct) =>
        await context.EmployeeAsync(ct) is { } employee && photos.PathOf(employee.PhotoPath) is { } path
            ? PhysicalFile(path, PhotoStore.ContentType(path))
            : NotFound();

    private async Task<IActionResult> ProfileView(Employee employee, MyProfileInput input, CancellationToken ct, ExperienceInput? experience = null)
    {
        var jobs = await db.EmployeeExperiences.Where(x => x.EmployeeId == employee.UniqueId).OrderByDescending(x => x.FromDate).ToListAsync(ct);
        return View("Profile", new MyProfilePage(employee, input, jobs, experience ?? new ExperienceInput()));
    }

    private async Task UpdatePriorExperienceAsync(Employee employee, CancellationToken ct)
    {
        employee.PriorExperienceMonths = Math.Min(720, (await db.EmployeeExperiences.Where(x => x.EmployeeId == employee.UniqueId).ToListAsync(ct)).Sum(x => x.Months));
        await db.SaveChangesAsync(ct);
    }

    // ---------- Resignation ----------

    public async Task<IActionResult> Resignation(CancellationToken ct)
    {
        if (await context.EmployeeAsync(ct) is not { } employee) return NoEmployee();
        return await ResignationView(employee, new ResignationInput
        {
            RequestedLastDay = DateOnly.FromDateTime(DateTime.Today).AddDays(employee.NoticePeriodDays),
        }, ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resignation(ResignationInput input, CancellationToken ct)
    {
        if (await context.EmployeeAsync(ct) is not { } employee) return NoEmployee();
        if (ModelState.IsValid && await resignations.SubmitAsync(employee, input.Reason, input.RequestedLastDay!.Value, ct) is { } error)
            ModelState.AddModelError(string.Empty, error);
        if (!ModelState.IsValid) return await ResignationView(employee, input, ct);

        TempData["Message"] = "Resignation submitted. HR will confirm your last working day.";
        return RedirectToAction(nameof(Resignation));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawResignation(int id, CancellationToken ct)
    {
        if (await context.EmployeeAsync(ct) is not { } employee) return NoEmployee();
        if (await resignations.WithdrawAsync(employee.UniqueId, id, ct)) TempData["Message"] = "Resignation withdrawn.";
        else TempData["Error"] = "Only a resignation that HR has not accepted yet can be withdrawn. Talk to HR.";
        return RedirectToAction(nameof(Resignation));
    }

    private async Task<IActionResult> ResignationView(Employee employee, ResignationInput input, CancellationToken ct) =>
        View("Resignation", new MyResignationPage(employee, await resignations.CurrentAsync(employee.UniqueId, ct),
            await resignations.HistoryAsync(employee.UniqueId, ct), input));

    // ---------- Helpers ----------

    private IActionResult NoEmployee() => View("NoEmployee");

    private Task<List<Attendance>> RecordsAsync(int employeeId, int year, int month, CancellationToken ct)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        return db.Attendances.Where(a => a.EmployeeId == employeeId && a.AttendanceDate >= first && a.AttendanceDate <= last).ToListAsync(ct);
    }
}
