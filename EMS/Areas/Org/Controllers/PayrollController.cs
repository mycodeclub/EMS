using EMS.Areas.Org.Models;
using EMS.Data;
using EMS.Services.Onboarding;
using EMS.Services.Payroll;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Areas.Org.Controllers;

/// <summary>Monthly salary sheet and printable salary slips, pro-rated from the attendance register.</summary>
public class PayrollController(OrganizationContext context, ApplicationDbContext db) : OrgController(context)
{
    public async Task<IActionResult> Index(int? year, int? month, CancellationToken ct)
    {
        var (y, m) = ResolveMonth(year, month);
        return View(new PayrollMonth(y, m, await LinesAsync(y, m, null, ct)));
    }

    public async Task<IActionResult> Slip(int id, int? year, int? month, CancellationToken ct)
    {
        var (y, m) = ResolveMonth(year, month);
        if ((await LinesAsync(y, m, id, ct)).FirstOrDefault() is not { } line) return NotFound();
        ViewData["Organization"] = Organization;
        return View(line);
    }

    private async Task<List<PayLine>> LinesAsync(int year, int month, int? employeeId, CancellationToken ct)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var employees = await db.Employees.Include(e => e.Shift)
            .Where(e => e.OrganizationId == Organization.UniqueId && e.DateOfJoining <= last && (e.DateOfLeaving == null || e.DateOfLeaving >= first))
            .Where(e => employeeId == null || e.UniqueId == employeeId)
            .OrderBy(e => e.EmpCode)
            .ToListAsync(ct);

        var ids = employees.Select(e => e.UniqueId).ToList();
        var records = (await db.Attendances
                .Where(a => ids.Contains(a.EmployeeId) && a.AttendanceDate >= first && a.AttendanceDate <= last)
                .ToListAsync(ct))
            .ToLookup(a => a.EmployeeId);

        return employees.Select(e => PayrollCalculator.Calculate(e, year, month, records[e.UniqueId])).ToList();
    }
}
