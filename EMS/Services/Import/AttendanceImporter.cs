using EMS.Data;
using EMS.Models;
using EMS.Services.Payroll;
using EMS.Services.Timekeeping;
using Microsoft.EntityFrameworkCore;

namespace EMS.Services.Import;

/// <summary>
/// Adds daily attendance (punch in / out and status) from a file. Days already marked are skipped, or replaced when asked.
/// Every row is checked first; if any row has a problem nothing is saved.
/// </summary>
public class AttendanceImporter(ApplicationDbContext db)
{
    public static readonly IReadOnlyList<ImportColumn> Columns =
    [
        new("code", "Employee code", true, "Employee code (E001…). Or use a Biometric ID column instead, as exported by the attendance device.", "E001", "Emp code", "EmpCode", "Code", "Employee ID", "Emp ID"),
        new("biometric", "Biometric ID", false, "Only needed when there is no Employee code column.", "", "Attendance ID", "Device ID", "Enroll ID", "Enrollment ID", "User ID"),
        new("date", "Date", true, "Day the shift started, e.g. 2026-09-05 or 05-09-2026 (day first). A night shift ending next morning goes on the day it started.", "2026-09-05", "Attendance date", "Work date"),
        new("in", "In time", false, "Punch in, 24-hour clock (09:05) or 9:05 AM.", "09:00", "In", "Punch in", "Check in", "Login", "Time in", "Clock in"),
        new("out", "Out time", false, "Punch out. If it is earlier than the in time, it is taken as the next morning.", "18:00", "Out", "Punch out", "Check out", "Logout", "Time out", "Clock out"),
        new("status", "Status", false, "P, A, HD, L, WO or H. Leave blank to work it out from the times and the shift.", "", "Attendance", "Attendance status"),
        new("remarks", "Remarks", false, "Optional note.", "", "Remark", "Note", "Notes", "Comment"),
    ];

    public async Task<ImportResult> ImportAsync(Organization organization, TabularFile file, string fileName, bool replaceExisting, CancellationToken ct = default)
    {
        var map = ColumnMap.Build(file, Columns);
        if (!map.Has("code") && !map.Has("biometric"))
            return ImportResult.Failed(ImportKind.Attendance, fileName, new ImportError(0, null, "Missing column: Employee code (or Biometric ID). Download the template to see the expected headings."));
        if (!map.Has("date"))
            return ImportResult.Failed(ImportKind.Attendance, fileName, new ImportError(0, null, "Missing column: Date. Download the template to see the expected headings."));
        if (!map.Has("in") && !map.Has("status"))
            return ImportResult.Failed(ImportKind.Attendance, fileName, new ImportError(0, null, "Add an In time column, a Status column, or both."));
        if (file.Rows.Count == 0) return ImportResult.Failed(ImportKind.Attendance, fileName, new ImportError(0, null, "The file has headings but no attendance rows."));

        var employees = await db.Employees.Include(e => e.Shift).Where(e => e.OrganizationId == organization.UniqueId).ToListAsync(ct);
        var byCode = employees.ToDictionary(e => e.EmpCode, StringComparer.OrdinalIgnoreCase);
        var byId = employees.ToDictionary(e => e.UniqueId);
        var assignments = map.Has("biometric")
            ? await db.BiometricIdAssignments.Where(a => a.OrganizationId == organization.UniqueId).ToListAsync(ct)
            : [];
        var today = DateOnly.FromDateTime(DateTime.Today);

        var errors = new List<ImportError>();
        var seen = new HashSet<(int, DateOnly)>();
        var parsed = new List<(Employee Employee, DateOnly Date, TimeOnly? In, TimeOnly? Out, AttendanceStatus? Status, string? Remarks)>();

        foreach (var row in file.Rows)
        {
            var before = errors.Count;
            void Error(string column, string message) => errors.Add(new(row.Number, column, message));

            DateOnly? date = null;
            if (map.Get(row, "date") is not { } dateText) Error("Date", "Required.");
            else if (!CellParser.TryParseDate(dateText, out var d)) Error("Date", $"\"{dateText}\" is not a date. Use 2026-09-05 or 05-09-2026 (day first).");
            else if (d > today) Error("Date", $"{d:dd MMM yyyy} is in the future.");
            else date = d;

            Employee? employee = null;
            if (map.Get(row, "code") is { } code)
            {
                if (!byCode.TryGetValue(code, out employee)) Error("Employee code", $"No employee with code {code}.");
            }
            else if (map.Get(row, "biometric") is { } deviceId)
            {
                if (date is { } on)
                {
                    var held = assignments.FirstOrDefault(a => a.AttendanceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase)
                                                               && a.ValidFrom <= on && (a.ValidTo == null || a.ValidTo >= on));
                    if (held is null || !byId.TryGetValue(held.EmployeeId, out employee)) Error("Biometric ID", $"No employee held biometric ID {deviceId} on {on:dd MMM yyyy}.");
                }
            }
            else Error("Employee code", "Required.");

            TimeOnly? timeIn = null, timeOut = null;
            if (map.Get(row, "in") is { } inText)
            {
                if (CellParser.TryParseTime(inText, out var t)) timeIn = t;
                else Error("In time", $"\"{inText}\" is not a time. Use 09:05 or 9:05 AM.");
            }
            if (map.Get(row, "out") is { } outText)
            {
                if (CellParser.TryParseTime(outText, out var t)) timeOut = t;
                else Error("Out time", $"\"{outText}\" is not a time. Use 18:30 or 6:30 PM.");
            }
            if (timeOut is not null && timeIn is null && map.Get(row, "in") is null) Error("In time", "An out time needs an in time.");
            if (timeIn is { } a && timeOut is { } b && a.Hour == b.Hour && a.Minute == b.Minute) Error("Out time", "Is the same as the in time.");

            AttendanceStatus? status = null;
            if (map.Get(row, "status") is { } statusText)
            {
                if (AttendanceCodes.TryParse(statusText, out var st)) status = st;
                else Error("Status", $"\"{statusText}\" is not a status. Use P, A, HD, L, WO or H.");
            }
            else if (timeIn is null && map.Get(row, "in") is null) Error("Status", "Enter an in time or a status.");

            var remarks = map.Get(row, "remarks");
            if (remarks is { Length: > 500 }) Error("Remarks", "Longer than 500 characters.");

            if (employee is not null && date is { } day)
            {
                var range = PayrollCalculator.EmployedRange(employee, day.Year, day.Month);
                if (range is not { } r || day < r.From || day > r.To)
                    Error("Date", $"{employee.EmpCode} was not on the rolls on {day:dd MMM yyyy}.");
                else if (!seen.Add((employee.UniqueId, day)))
                    Error("Date", $"{employee.EmpCode} has more than one row for {day:dd MMM yyyy}.");
            }

            if (errors.Count == before) parsed.Add((employee!, date!.Value, timeIn, timeOut, status, remarks));
        }

        var notes = new List<string>();
        if (map.Unknown.Count > 0) notes.Add($"Ignored column{(map.Unknown.Count == 1 ? "" : "s")}: {string.Join(", ", map.Unknown)}.");
        if (errors.Count > 0) return new(ImportKind.Attendance, fileName, 0, 0, 0, errors, notes);

        var ids = parsed.Select(p => p.Employee.UniqueId).Distinct().ToList();
        var from = parsed.Min(p => p.Date);
        var to = parsed.Max(p => p.Date);
        var existing = (await db.Attendances
                .Where(x => ids.Contains(x.EmployeeId) && x.AttendanceDate >= from && x.AttendanceDate <= to)
                .ToListAsync(ct))
            .GroupBy(x => (x.EmployeeId, x.AttendanceDate))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.UniqueId).First());

        int added = 0, updated = 0, skipped = 0;
        foreach (var (employee, date, timeIn, timeOut, status, remarks) in parsed)
        {
            existing.TryGetValue((employee.UniqueId, date), out var record);
            if (record is not null && !replaceExisting)
            {
                skipped++;
                continue;
            }

            var result = PunchRules.Evaluate(employee.Shift, date, timeIn, timeOut);
            if (record is null)
            {
                record = new Attendance { EmployeeId = employee.UniqueId, ShiftId = employee.ShiftId, AttendanceDate = date };
                db.Attendances.Add(record);
                added++;
            }
            else updated++;

            record.Status = (status ?? result.Status)!.Value;
            record.LoginAt = result.LoginAt;
            record.LogoffAt = result.LogoffAt;
            record.WorkedMinutes = result.WorkedMinutes;
            record.IsLate = result.IsLate;
            record.Remarks = remarks;
            record.Source = AttendanceSource.Import;
        }

        await db.SaveChangesAsync(ct);
        if (skipped > 0) notes.Add($"{skipped} day{(skipped == 1 ? " was" : "s were")} already marked and left unchanged. Tick \"Replace days already marked\" to overwrite them.");
        return new(ImportKind.Attendance, fileName, added, updated, skipped, [], notes);
    }
}
