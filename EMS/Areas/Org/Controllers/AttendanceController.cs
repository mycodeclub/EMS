using EMS.Areas.Org.Models;
using EMS.Data;
using EMS.Models;
using EMS.Services.Onboarding;
using EMS.Services.Payroll;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Areas.Org.Controllers;

/// <summary>Monthly attendance register. Any past month can be filled in or corrected; future days are locked.</summary>
public class AttendanceController(OrganizationContext context, ApplicationDbContext db) : OrgController(context)
{
    public async Task<IActionResult> Index(int? year, int? month, CancellationToken ct)
    {
        var (y, m) = ResolveMonth(year, month);
        var employees = await EmployeesOnRollsAsync(y, m, ct);
        var marks = (await RecordsAsync(employees, y, m, ct))
            .GroupBy(a => (a.EmployeeId, a.AttendanceDate.Day))
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.UniqueId).First().Status);

        return View(new AttendanceSheet(y, m, employees, marks, Organization.OperatesInShifts == true));
    }

    /// <summary>Cells are posted as m{employeeId}_{day} = (int)AttendanceStatus, or empty to clear the day.</summary>
    [HttpPost, ValidateAntiForgeryToken, RequestFormLimits(ValueCountLimit = 20_000)]
    public async Task<IActionResult> Index(int year, int month, CancellationToken ct)
    {
        var (y, m) = ResolveMonth(year, month);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var employees = await EmployeesOnRollsAsync(y, m, ct);
        var existing = (await RecordsAsync(employees, y, m, ct))
            .GroupBy(a => (a.EmployeeId, a.AttendanceDate))
            .ToDictionary(g => g.Key, g => g.ToList());
        var changed = 0;

        foreach (var employee in employees)
        {
            var (from, to) = PayrollCalculator.EmployedRange(employee, y, m)!.Value;
            for (var day = from; day <= to && day <= today; day = day.AddDays(1))
            {
                if (!Request.Form.TryGetValue($"m{employee.UniqueId}_{day.Day}", out var posted)) continue;
                AttendanceStatus? status = int.TryParse(posted, out var value) && Enum.IsDefined((AttendanceStatus)value) ? (AttendanceStatus)value : null;
                var records = existing.GetValueOrDefault((employee.UniqueId, day)) ?? [];

                if (status is null)
                {
                    if (records.Count == 0) continue;
                    db.Attendances.RemoveRange(records); // soft delete
                }
                else if (records.Count == 0)
                {
                    db.Attendances.Add(new Attendance
                    {
                        EmployeeId = employee.UniqueId, ShiftId = employee.ShiftId, AttendanceDate = day,
                        Status = status.Value, Source = AttendanceSource.Manual,
                    });
                }
                else if (records[0].Status != status)
                {
                    records[0].Status = status.Value;
                    records[0].Source = AttendanceSource.Manual;
                }
                else continue;
                changed++;
            }
        }

        await db.SaveChangesAsync(ct);
        TempData["Message"] = changed == 0 ? "No changes to save." : $"Attendance saved ({changed} day{(changed == 1 ? "" : "s")} updated).";
        return RedirectToAction(nameof(Index), new { year = y, month = m });
    }

    private async Task<List<Employee>> EmployeesOnRollsAsync(int year, int month, CancellationToken ct)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        return await db.Employees.Include(e => e.Shift)
            .Where(e => e.OrganizationId == Organization.UniqueId && e.DateOfJoining <= last && (e.DateOfLeaving == null || e.DateOfLeaving >= first))
            .OrderBy(e => e.EmpCode)
            .ToListAsync(ct);
    }

    private async Task<List<Attendance>> RecordsAsync(List<Employee> employees, int year, int month, CancellationToken ct)
    {
        var ids = employees.Select(e => e.UniqueId).ToList();
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        return await db.Attendances
            .Where(a => ids.Contains(a.EmployeeId) && a.AttendanceDate >= first && a.AttendanceDate <= last)
            .ToListAsync(ct);
    }
}
