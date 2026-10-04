using Microsoft.AspNetCore.Authorization;
using EMS.Models.Common;
using EMS.Areas.Org.Models;
using EMS.Data;
using EMS.Models;
using EMS.Services.Import;
using EMS.Services.Onboarding;
using EMS.Services.Payroll;
using EMS.Services.Timekeeping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Areas.Org.Controllers;

/// <summary>
/// Monthly attendance register and the day view for punch in / out times.
/// Any past day can be filled in or corrected; future days are locked.
/// </summary>
[Authorize(Roles = AppRoles.PeopleManagers)]
public class AttendanceController(OrganizationContext context, ApplicationDbContext db) : OrgController(context)
{
    public async Task<IActionResult> Index(int? year, int? month, CancellationToken ct)
    {
        var (y, m) = ResolveMonth(year, month);
        var employees = await EmployeesOnRollsAsync(y, m, ct);
        var marks = (await RecordsAsync(employees, y, m, ct))
            .GroupBy(a => (a.EmployeeId, a.AttendanceDate.Day))
            .Select(g => (g.Key, Record: g.OrderBy(a => a.UniqueId).First()))
            .ToDictionary(x => x.Key, x => new DayMark(x.Record.Status, x.Record.LoginAt, x.Record.LogoffAt, x.Record.WorkedMinutes, x.Record.IsLate));

        return View(new AttendanceSheet(y, m, employees, marks, Organization.OperatesInShifts == true));
    }

    /// <summary>Cells are posted as m{employeeId}_{day} = (int)AttendanceStatus, or empty to clear the day.</summary>
    [HttpPost, ValidateAntiForgeryToken, RequestFormLimits(ValueCountLimit = 20_000)]
    public async Task<IActionResult> Index(int year, int month, CancellationToken ct)
    {
        var (y, m) = ResolveMonth(year, month);
        var today = DateOnly.FromDateTime(AppClock.Today);
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

    /// <summary>Punch in / out for every employee on one day. Defaults to today.</summary>
    public async Task<IActionResult> Day(DateOnly? date, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(AppClock.Today);
        var day = date is { } d && d <= today ? d : today;
        var employees = await EmployeesOnRollsAsync(day, day, ct);
        var records = await FirstRecordsAsync(employees, day, ct);

        var entries = employees.Select(e =>
        {
            var record = records.GetValueOrDefault(e.UniqueId);
            return new PunchEntry
            {
                Employee = e,
                In = record?.LoginAt is { } login ? PunchRules.Clock(login) : null,
                Out = record?.LogoffAt is { } logoff ? PunchRules.Clock(logoff) : null,
                Status = record?.Status,
                Remarks = record?.Remarks,
                WorkedMinutes = record?.WorkedMinutes,
                IsLate = record?.IsLate ?? false,
            };
        }).ToList();
        return View(new AttendanceDay(day, entries, Organization.OperatesInShifts == true));
    }

    /// <summary>
    /// Rows are posted as in{id}, out{id}, s{id} (status) and r{id} (remarks). A blank status with an in time is worked out
    /// from the shift; a row left completely blank clears the day. If any row has a problem, nothing is saved.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken, ActionName("Day"), RequestFormLimits(ValueCountLimit = 20_000)]
    public async Task<IActionResult> SaveDay(DateOnly date, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(AppClock.Today);
        if (date > today) return RedirectToAction(nameof(Day));

        var employees = await EmployeesOnRollsAsync(date, date, ct);
        var records = await FirstRecordsAsync(employees, date, ct);
        var entries = new List<PunchEntry>();
        var changes = new List<Action>();

        foreach (var employee in employees)
        {
            string? Posted(string prefix) => Request.Form.TryGetValue($"{prefix}{employee.UniqueId}", out var v) && !string.IsNullOrWhiteSpace(v) ? v.ToString().Trim() : null;
            var entry = new PunchEntry
            {
                Employee = employee, In = Posted("in"), Out = Posted("out"), Remarks = Posted("r"),
                Status = int.TryParse(Posted("s"), out var sv) && Enum.IsDefined((AttendanceStatus)sv) ? (AttendanceStatus)sv : null,
            };
            entries.Add(entry);

            TimeOnly? timeIn = null, timeOut = null;
            if (entry.In is not null) { if (CellParser.TryParseTime(entry.In, out var t)) timeIn = t; else entry.Error = "Enter the in time as 09:05."; }
            if (entry.Out is not null) { if (CellParser.TryParseTime(entry.Out, out var t)) timeOut = t; else entry.Error = "Enter the out time as 18:30."; }
            if (entry.Error is null && timeOut is not null && timeIn is null) entry.Error = "An out time needs an in time.";
            if (entry.Error is null && timeIn is { } a && timeOut is { } b && a.Hour == b.Hour && a.Minute == b.Minute) entry.Error = "The out time is the same as the in time.";
            if (entry.Error is null && entry.Status is null && timeIn is null && entry.Remarks is not null) entry.Error = "Choose a status or enter an in time.";
            if (entry.Remarks is { Length: > 500 }) entry.Error = "Remarks can be at most 500 characters.";
            if (entry.Error is not null) continue;

            var result = PunchRules.Evaluate(employee.Shift, date, timeIn, timeOut);
            entry.WorkedMinutes = result.WorkedMinutes;
            entry.IsLate = result.IsLate;
            var status = entry.Status ?? result.Status;
            var record = records.GetValueOrDefault(employee.UniqueId);

            if (status is null)
            {
                if (record is not null) changes.Add(() => db.Attendances.Remove(record)); // soft delete
                continue;
            }
            if (record is not null && record.Status == status && record.LoginAt == result.LoginAt && record.LogoffAt == result.LogoffAt
                && record.Remarks == entry.Remarks) continue;

            changes.Add(() =>
            {
                if (record is null)
                {
                    record = new Attendance { EmployeeId = employee.UniqueId, ShiftId = employee.ShiftId, AttendanceDate = date };
                    db.Attendances.Add(record);
                }
                record.Status = status.Value;
                record.LoginAt = result.LoginAt;
                record.LogoffAt = result.LogoffAt;
                record.WorkedMinutes = result.WorkedMinutes;
                record.IsLate = result.IsLate;
                record.Remarks = entry.Remarks;
                record.Source = AttendanceSource.Manual;
            });
        }

        var problems = entries.Count(e => e.Error is not null);
        if (problems > 0)
        {
            TempData["Error"] = $"Nothing was saved: {problems} row{(problems == 1 ? " has a problem" : "s have problems")}. Fix the highlighted rows and save again.";
            return View("Day", new AttendanceDay(date, entries, Organization.OperatesInShifts == true));
        }

        changes.ForEach(change => change());
        await db.SaveChangesAsync(ct);
        TempData["Message"] = changes.Count == 0 ? "No changes to save."
            : $"Attendance for {date:dd MMM yyyy} saved ({changes.Count} employee{(changes.Count == 1 ? "" : "s")} updated).";
        return RedirectToAction(nameof(Day), new { date = date.ToString("yyyy-MM-dd") });
    }

    private async Task<Dictionary<int, Attendance>> FirstRecordsAsync(List<Employee> employees, DateOnly date, CancellationToken ct)
    {
        var ids = employees.Select(e => e.UniqueId).ToList();
        return (await db.Attendances.Where(a => ids.Contains(a.EmployeeId) && a.AttendanceDate == date).ToListAsync(ct))
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.UniqueId).First());
    }

    private Task<List<Employee>> EmployeesOnRollsAsync(int year, int month, CancellationToken ct)
    {
        var first = new DateOnly(year, month, 1);
        return EmployeesOnRollsAsync(first, first.AddMonths(1).AddDays(-1), ct);
    }

    private async Task<List<Employee>> EmployeesOnRollsAsync(DateOnly first, DateOnly last, CancellationToken ct) =>
        await db.Employees.Include(e => e.Shift)
            .Where(e => e.OrganizationId == Organization.UniqueId && e.DateOfJoining <= last && (e.DateOfLeaving == null || e.DateOfLeaving >= first))
            .OrderBy(e => e.EmpCode)
            .ToListAsync(ct);

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
