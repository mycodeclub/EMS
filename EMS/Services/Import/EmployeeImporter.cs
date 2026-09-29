using System.ComponentModel.DataAnnotations;
using EMS.Data;
using EMS.Models;
using EMS.Services.Onboarding;
using Microsoft.EntityFrameworkCore;

namespace EMS.Services.Import;

/// <summary>
/// Adds employees from a file, and optionally updates the ones whose code already exists.
/// Every row is checked first; if any row has a problem nothing is saved.
/// </summary>
public class EmployeeImporter(ApplicationDbContext db, SetupService setup)
{
    public static readonly IReadOnlyList<ImportColumn> Columns =
    [
        new("code", "Employee code", false, "Leave blank to number new employees automatically (E001, E002…). Codes are never reused.", "", "Emp code", "EmpCode", "Code", "Employee ID", "Emp ID"),
        new("first", "First name", true, "", "Sample", "FirstName", "Name"),
        new("last", "Last name", false, "", "Employee", "LastName", "Surname"),
        new("gender", "Gender", false, "Male, Female or Other.", "Female"),
        new("dob", "Date of birth", false, "Date, e.g. 2026-09-05 or 05-09-2026 (day first).", "1994-06-15", "DOB", "Birth date"),
        new("mobile", "Mobile", false, "", "+91 90000 00001", "Phone", "Mobile number", "Phone number"),
        new("email", "Email", false, "", "employee1@example.com", "Email address", "Email ID"),
        new("department", "Department", false, "", "Operations", "Dept"),
        new("designation", "Designation", false, "", "Executive", "Title", "Job title", "Role"),
        new("joined", "Date of joining", true, "Date the employee started.", "2026-04-01", "DOJ", "Joining date", "Joined on"),
        new("left", "Date of leaving", false, "Only for employees who have left.", "", "DOL", "Leaving date", "Relieved on"),
        new("shift", "Shift", false, "Shift code or name from your Shifts page. Blank = no fixed shift.", "", "Shift code", "Shift name"),
        new("salary", "Monthly salary", false, "Gross monthly salary in rupees.", "25000", "Salary", "Gross salary", "CTC per month"),
        new("biometric", "Biometric ID", false, "Enrolment ID on the attendance device. Blank = same as the employee code.", "", "Attendance ID", "Device ID", "Enroll ID", "Enrollment ID"),
    ];

    public async Task<ImportResult> ImportAsync(Organization organization, TabularFile file, string fileName, bool updateExisting, CancellationToken ct = default)
    {
        var map = ColumnMap.Build(file, Columns);
        // First name and joining date are only needed for new employees, so a file that updates existing ones by code can leave them out.
        if (!map.Has("code") && !map.Has("first"))
            return ImportResult.Failed(ImportKind.Employees, fileName, new ImportError(0, null, "Missing column: First name (or Employee code, to update existing employees). Download the template to see the expected headings."));
        if (file.Rows.Count == 0) return ImportResult.Failed(ImportKind.Employees, fileName, new ImportError(0, null, "The file has headings but no employee rows."));

        var shifts = await setup.ShiftsAsync(organization.UniqueId, ct);
        var everyCode = await setup.EmployeeCodesAsync(organization.UniqueId, ct); // includes removed employees
        var current = await db.Employees.Where(e => e.OrganizationId == organization.UniqueId).ToDictionaryAsync(e => e.EmpCode, StringComparer.OrdinalIgnoreCase, ct);
        var branch = OrganizationContext.DefaultBranch(organization);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var errors = new List<ImportError>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<(FileRow Row, Employee? Existing, Action<Employee> Apply)>();
        var skipped = 0;

        foreach (var row in file.Rows)
        {
            var rowErrors = new List<ImportError>();
            void Error(string column, string message) => rowErrors.Add(new(row.Number, column, message));
            string? Text(string key, int max, string header)
            {
                var value = map.Get(row, key);
                if (value is not null && value.Length > max) Error(header, $"Longer than {max} characters.");
                return value;
            }
            DateOnly? Date(string key, string header)
            {
                var value = map.Get(row, key);
                if (value is null) return null;
                if (CellParser.TryParseDate(value, out var date)) return date;
                Error(header, $"\"{value}\" is not a date. Use 2026-09-05 or 05-09-2026 (day first).");
                return null;
            }

            var code = Text("code", 20, "Employee code")?.ToUpperInvariant();
            current.TryGetValue(code ?? "", out var existing);
            if (code is not null)
            {
                if (!seen.Add(code)) Error("Employee code", $"{code} appears more than once in the file.");
                else if (existing is null && everyCode.Contains(code)) Error("Employee code", $"{code} belonged to a removed employee. Codes are never reused.");
                else if (existing is not null && !updateExisting)
                {
                    skipped++;
                    continue;
                }
            }

            var first = Text("first", 100, "First name");
            var last = Text("last", 100, "Last name");
            var mobile = Text("mobile", 20, "Mobile");
            var email = Text("email", 150, "Email");
            var department = Text("department", 100, "Department");
            var designation = Text("designation", 100, "Designation");
            var biometric = Text("biometric", 20, "Biometric ID")?.ToUpperInvariant();
            var dob = Date("dob", "Date of birth");
            var joined = Date("joined", "Date of joining");
            var left = Date("left", "Date of leaving");

            if (existing is null && first is null) Error("First name", "Required for a new employee.");
            if (existing is null && joined is null && map.Get(row, "joined") is null) Error("Date of joining", "Required for a new employee.");
            if (email is not null && !new EmailAddressAttribute().IsValid(email)) Error("Email", $"\"{email}\" is not an email address.");
            if (mobile is not null && !new PhoneAttribute().IsValid(mobile)) Error("Mobile", $"\"{mobile}\" is not a phone number.");
            if (dob is { } born && born >= today) Error("Date of birth", "Must be in the past.");
            if ((left ?? existing?.DateOfLeaving) is { } leaving && (joined ?? existing?.DateOfJoining) is { } joining && leaving < joining)
                Error("Date of leaving", "Is before the date of joining.");

            Gender? gender = null;
            if (map.Get(row, "gender") is { } g)
            {
                var match = Enum.GetValues<Gender>().Where(v => v.ToString().StartsWith(g.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                if (match.Count == 1) gender = match[0];
                else Error("Gender", $"\"{g}\" is not Male, Female or Other.");
            }

            Shift? shift = null;
            if (map.Get(row, "shift") is { } s)
            {
                shift = shifts.FirstOrDefault(x => x.Code.Equals(s, StringComparison.OrdinalIgnoreCase))
                        ?? shifts.FirstOrDefault(x => x.Name.Equals(s, StringComparison.OrdinalIgnoreCase));
                if (shift is null)
                    Error("Shift", shifts.Count == 0 ? "You have no shifts yet. Add them on the Shifts page, or leave this blank."
                        : $"\"{s}\" is not one of your shifts ({string.Join(", ", shifts.Select(x => x.Code))}).");
            }

            decimal? salary = null;
            if (map.Get(row, "salary") is { } pay)
            {
                if (CellParser.TryParseDecimal(pay, out var amount) && amount is >= 0 and <= 10_000_000) salary = amount;
                else Error("Monthly salary", $"\"{pay}\" is not an amount between 0 and 1,00,00,000.");
            }

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }

            // Blank cells keep an existing employee's current value.
            pending.Add((row, existing, e =>
            {
                if (first is not null) e.FirstName = first;
                if (last is not null) e.LastName = last;
                if (gender is not null) e.Gender = gender;
                if (dob is not null) e.DateOfBirth = dob;
                if (mobile is not null) e.Mobile = mobile;
                if (email is not null) e.Email = email;
                if (department is not null) e.Department = department;
                if (designation is not null) e.Designation = designation;
                if (joined is not null) e.DateOfJoining = joined.Value;
                if (left is not null)
                {
                    e.DateOfLeaving = left;
                    if (left < today && e.Status is EmployeeStatus.Active or EmployeeStatus.OnProbation or EmployeeStatus.OnNotice) e.Status = EmployeeStatus.Resigned;
                }
                if (shift is not null) e.ShiftId = shift.UniqueId;
                if (salary is not null) e.MonthlySalary = salary;
                if (biometric is not null) e.AttendanceId = biometric;
            }));
        }

        var notes = new List<string>();
        if (map.Unknown.Count > 0) notes.Add($"Ignored column{(map.Unknown.Count == 1 ? "" : "s")}: {string.Join(", ", map.Unknown)}.");
        if (skipped > 0) notes.Add($"{skipped} row{(skipped == 1 ? "" : "s")} skipped because the employee code already exists. Tick \"Update existing employees\" to update them instead.");
        if (errors.Count > 0) return new(ImportKind.Employees, fileName, 0, 0, skipped, errors, notes);

        // Blank codes are numbered after every code in use or in the file.
        everyCode.UnionWith(seen);
        int added = 0, updated = 0;
        foreach (var (row, existing, apply) in pending)
        {
            if (existing is not null)
            {
                apply(existing);
                updated++;
                continue;
            }

            var code = map.Get(row, "code")?.ToUpperInvariant() ?? SetupService.NextCode(everyCode);
            everyCode.Add(code);
            var employee = new Employee { OrganizationId = organization.UniqueId, BranchId = branch.UniqueId, EmpCode = code };
            apply(employee);
            db.Employees.Add(employee);
            added++;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (BusinessRuleException ex)
        {
            db.ChangeTracker.Clear();
            return new(ImportKind.Employees, fileName, 0, 0, skipped, [new ImportError(0, "Biometric ID", ex.Message)], notes);
        }

        return new(ImportKind.Employees, fileName, added, updated, skipped, [], notes);
    }
}
