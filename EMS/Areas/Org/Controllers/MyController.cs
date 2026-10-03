using EMS.Areas.Org.Models;
using EMS.Data;
using EMS.Models.Common;
using EMS.Services.Onboarding;
using EMS.Services.Payroll;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Areas.Org.Controllers;

/// <summary>Self-service for staff with a login: their own attendance for a month and their salary slip.</summary>
[Authorize(Roles = AppRoles.SelfService)]
public class MyController(OrganizationContext context, ApplicationDbContext db) : OrgController(context)
{
    private readonly OrganizationContext context = context;

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

    private Task<List<EMS.Models.Attendance>> RecordsAsync(int employeeId, int year, int month, CancellationToken ct)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        return db.Attendances.Where(a => a.EmployeeId == employeeId && a.AttendanceDate >= first && a.AttendanceDate <= last).ToListAsync(ct);
    }
}
