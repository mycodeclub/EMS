using System.ComponentModel.DataAnnotations;
using EMS.Models;
using EMS.Models.Common;
using EMS.Services.Import;
using EMS.Services.Payroll;
using EMS.Services.Timekeeping;

namespace EMS.Areas.Org.Models;

public record OrgDashboard(
    int Staff, int PresentToday, int AbsentToday, int UnmarkedToday, decimal MonthlyPayroll,
    StackedChart Attendance, BarChart ByShift, IReadOnlyList<Employee> RecentJoiners);

/// <summary>Month register: one row per employee on the rolls, one cell per day.</summary>
public record AttendanceSheet(
    int Year, int Month, IReadOnlyList<Employee> Employees,
    IReadOnlyDictionary<(int EmployeeId, int Day), DayMark> Marks, bool RoundTheClock)
{
    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);
    public DateOnly First => new(Year, Month, 1);

    /// <summary>Short codes used in the register.</summary>
    public static readonly (AttendanceStatus Status, string Code, string Name)[] Codes = AttendanceCodes.All;
}

/// <summary>A marked day in the register, with the punch times when there are any.</summary>
public record DayMark(AttendanceStatus Status, DateTime? LoginAt, DateTime? LogoffAt, int? WorkedMinutes, bool IsLate)
{
    /// <summary>"In 09:02 · Out 18:10 · 9h 08m · Late" for the cell tooltip, or null without punch times.</summary>
    public string? Summary => LoginAt is not { } login ? null : string.Join(" · ", new[]
    {
        $"In {PunchRules.Clock(login)}",
        LogoffAt is { } logoff ? $"Out {PunchRules.Clock(logoff)}" : "No out time",
        WorkedMinutes is { } worked ? PunchRules.Duration(worked) : null,
        IsLate ? "Late" : null,
    }.Where(p => p is not null));
}

/// <summary>Day view: punch in / out and status for every employee on the rolls that day.</summary>
public record AttendanceDay(DateOnly Date, IReadOnlyList<PunchEntry> Entries, bool RoundTheClock)
{
    public bool IsToday => Date == DateOnly.FromDateTime(DateTime.Today);
}

/// <summary>One employee's row in the day view: the saved record, or what was just posted when it had a problem.</summary>
public class PunchEntry
{
    public required Employee Employee { get; init; }
    public string? In { get; set; }
    public string? Out { get; set; }
    public AttendanceStatus? Status { get; set; }
    public string? Remarks { get; set; }
    public int? WorkedMinutes { get; set; }
    public bool IsLate { get; set; }
    public string? Error { get; set; }
}

/// <summary>Import page: the result of the last upload (if any) and what the templates need to mention.</summary>
public record ImportPage(ImportResult? Result, int EmployeeCount, IReadOnlyList<string> ShiftCodes);

public record PayrollMonth(int Year, int Month, IReadOnlyList<PayLine> Lines)
{
    public DateOnly First => new(Year, Month, 1);
}

/// <summary>Self-service: the signed-in employee's days in a month (up to today) and pay for it.</summary>
public record MyMonth(int Year, int Month, Employee? Employee, IReadOnlyList<MyDay> Days, PayLine? Pay)
{
    public DateOnly First => new(Year, Month, 1);
}

/// <summary>One day on the rolls; Record is null when the day is not marked yet.</summary>
public record MyDay(DateOnly Date, Attendance? Record);

public class EmployeeInput
{
    public int? Id { get; set; }

    [StringLength(20), Display(Name = "Employee code")]
    public string? EmpCode { get; set; }

    [Required, StringLength(100), Display(Name = "First name")] public string FirstName { get; set; } = string.Empty;
    [StringLength(100), Display(Name = "Last name")] public string? LastName { get; set; }
    public Gender? Gender { get; set; }
    [Display(Name = "Date of birth")] public DateOnly? DateOfBirth { get; set; }
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    [Phone, StringLength(20)] public string? Mobile { get; set; }
    [StringLength(100)] public string? Department { get; set; }
    [StringLength(100)] public string? Designation { get; set; }
    [Required, Display(Name = "Date of joining")] public DateOnly? DateOfJoining { get; set; }
    [Display(Name = "Date of leaving")] public DateOnly? DateOfLeaving { get; set; }
    [Display(Name = "Shift")] public int? ShiftId { get; set; }
    [Range(0, 10_000_000), Display(Name = "Monthly salary (₹)")] public decimal? MonthlySalary { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public static EmployeeInput From(Employee e) => new()
    {
        Id = e.UniqueId, EmpCode = e.EmpCode, FirstName = e.FirstName, LastName = e.LastName, Gender = e.Gender,
        DateOfBirth = e.DateOfBirth, Email = e.Email, Mobile = e.Mobile, Department = e.Department, Designation = e.Designation,
        DateOfJoining = e.DateOfJoining, DateOfLeaving = e.DateOfLeaving, ShiftId = e.ShiftId, MonthlySalary = e.MonthlySalary, Status = e.Status,
    };

    public void ApplyTo(Employee e)
    {
        e.FirstName = FirstName.Trim();
        e.LastName = LastName?.Trim();
        e.Gender = Gender;
        e.DateOfBirth = DateOfBirth;
        e.Email = Email?.Trim();
        e.Mobile = Mobile?.Trim();
        e.Department = Department?.Trim();
        e.Designation = Designation?.Trim();
        e.DateOfJoining = DateOfJoining!.Value;
        e.DateOfLeaving = DateOfLeaving;
        e.ShiftId = ShiftId;
        e.MonthlySalary = MonthlySalary;
        e.Status = Status;
    }
}
