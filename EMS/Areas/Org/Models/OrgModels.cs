using System.ComponentModel.DataAnnotations;
using EMS.Models;
using EMS.Models.Common;
using EMS.Services.Payroll;

namespace EMS.Areas.Org.Models;

public record OrgDashboard(
    int Staff, int PresentToday, int AbsentToday, int UnmarkedToday, decimal MonthlyPayroll,
    StackedChart Attendance, BarChart ByShift, IReadOnlyList<Employee> RecentJoiners);

/// <summary>Month register: one row per employee on the rolls, one cell per day.</summary>
public record AttendanceSheet(
    int Year, int Month, IReadOnlyList<Employee> Employees,
    IReadOnlyDictionary<(int EmployeeId, int Day), AttendanceStatus> Marks, bool RoundTheClock)
{
    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);
    public DateOnly First => new(Year, Month, 1);

    /// <summary>Short codes used in the register.</summary>
    public static readonly (AttendanceStatus Status, string Code, string Name)[] Codes =
    [
        (AttendanceStatus.Present, "P", "Present"),
        (AttendanceStatus.Absent, "A", "Absent"),
        (AttendanceStatus.HalfDay, "HD", "Half day"),
        (AttendanceStatus.OnLeave, "L", "On leave"),
        (AttendanceStatus.WeeklyOff, "WO", "Weekly off"),
        (AttendanceStatus.Holiday, "H", "Holiday"),
    ];
}

public record PayrollMonth(int Year, int Month, IReadOnlyList<PayLine> Lines)
{
    public DateOnly First => new(Year, Month, 1);
}

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
