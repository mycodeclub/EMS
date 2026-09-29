using System.Globalization;
using EMS.Areas.Org.Models;
using EMS.Data;
using EMS.Models;
using EMS.Models.Common;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Areas.Org.Controllers;

public class DashboardController(OrganizationContext context, ApplicationDbContext db) : OrgController(context)
{
    private const int ChartDays = 14;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = today.AddDays(1 - ChartDays);

        var employees = await db.Employees.Include(e => e.Shift)
            .Where(e => e.OrganizationId == Organization.UniqueId)
            .ToListAsync(ct);
        var onRolls = employees.Where(e => OnRolls(e, today)).ToList();

        // One status per employee per day.
        var statuses = (await db.Attendances
                .Where(a => a.Employee.OrganizationId == Organization.UniqueId && a.AttendanceDate >= from && a.AttendanceDate <= today)
                .Select(a => new { a.EmployeeId, a.AttendanceDate, a.Status, a.UniqueId })
                .ToListAsync(ct))
            .GroupBy(a => (a.EmployeeId, a.AttendanceDate))
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.UniqueId).First().Status);

        int Count(DateOnly day, params AttendanceStatus[] wanted) =>
            statuses.Count(s => s.Key.AttendanceDate == day && wanted.Contains(s.Value));

        var columns = Enumerable.Range(0, ChartDays).Select(i => from.AddDays(i)).Select(day => new StackedColumn(
            day.ToString("dd MMM", CultureInfo.InvariantCulture),
            [
                Count(day, AttendanceStatus.Present, AttendanceStatus.HalfDay),
                Count(day, AttendanceStatus.OnLeave, AttendanceStatus.WeeklyOff, AttendanceStatus.Holiday),
                Count(day, AttendanceStatus.Absent),
            ])).ToList();

        var markedToday = statuses.Keys.Count(k => k.AttendanceDate == today && onRolls.Any(e => e.UniqueId == k.EmployeeId));
        var byShift = onRolls
            .GroupBy(e => e.Shift?.Name ?? "No fixed shift")
            .Select(g => new ChartPoint(g.Key, g.Count(), $"{g.Count()} staff"))
            .OrderByDescending(p => p.Value)
            .ToList();

        return View(new OrgDashboard(
            Staff: onRolls.Count,
            PresentToday: Count(today, AttendanceStatus.Present, AttendanceStatus.HalfDay),
            AbsentToday: Count(today, AttendanceStatus.Absent),
            UnmarkedToday: onRolls.Count - markedToday,
            MonthlyPayroll: onRolls.Sum(e => e.MonthlySalary ?? 0),
            Attendance: new StackedChart("Attendance, last 14 days", "Staff per day by status", ["Present", "Leave / off", "Absent"], columns),
            ByShift: new BarChart("Staff by shift", "Employees currently on the rolls", byShift),
            RecentJoiners: onRolls.OrderByDescending(e => e.DateOfJoining).ThenByDescending(e => e.UniqueId).Take(5).ToList()));
    }

    private static bool OnRolls(Employee e, DateOnly day) => e.DateOfJoining <= day && (e.DateOfLeaving is null || e.DateOfLeaving >= day);
}
