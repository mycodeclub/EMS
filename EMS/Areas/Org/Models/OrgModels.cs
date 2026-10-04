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

public record MyLeavePage(Employee Employee, int Year, IReadOnlyList<EMS.Services.Leave.LeaveBalanceRow> Balances,
    IReadOnlyList<LeaveApplication> Applications, EMS.Services.Leave.LeaveRequestInput Input);

public record MyProfilePage(Employee Employee, MyProfileInput Input, IReadOnlyList<EmployeeExperience> Experiences, ExperienceInput NewExperience);

public record MyResignationPage(Employee Employee, Resignation? Current, IReadOnlyList<Resignation> History, ResignationInput Input);

/// <summary>Letters available for an employee: offer, one appraisal letter per salary revision, relieving once a resignation is accepted.</summary>
public record LettersPage(Employee Employee, IReadOnlyList<SalaryRevision> Revisions, Resignation? Accepted, bool IsSelf);

/// <summary>HR's queue: pending leave, open resignations and recent leave decisions.</summary>
public record RequestsPage(IReadOnlyList<LeaveApplication> PendingLeave, IReadOnlyList<Resignation> Resignations, IReadOnlyList<LeaveApplication> RecentLeave,
    IReadOnlyList<EmployeeDocument> DocumentsToVerify);

/// <summary>Self-service joining checklist: what is still needed, and the documents uploaded so far.</summary>
public record MyChecklistPage(Employee Employee, EMS.Services.People.Checklist Checklist, IReadOnlyList<EmployeeDocument> Documents);

/// <summary>What an employee can change on their own profile. Blank PAN / Aadhaar keep the saved value. PAN and IFSC are accepted in any case and saved in capitals.</summary>
public class MyProfileInput
{
    public Gender? Gender { get; set; }
    [Display(Name = "Date of birth")] public DateOnly? DateOfBirth { get; set; }
    [Phone, StringLength(20)] public string? Mobile { get; set; }
    [EmailAddress, StringLength(150), Display(Name = "Contact email")] public string? Email { get; set; }
    [StringLength(150), Display(Name = "Highest qualification")] public string? HighestQualification { get; set; }
    [StringLength(500), Display(Name = "Current address")] public string? CurrentAddress { get; set; }
    [StringLength(150), Display(Name = "Emergency contact name")] public string? EmergencyContactName { get; set; }
    [Phone, StringLength(20), Display(Name = "Emergency contact phone")] public string? EmergencyContactPhone { get; set; }

    [StringLength(10), RegularExpression("^[A-Za-z]{5}[0-9]{4}[A-Za-z]$", ErrorMessage = "Enter the PAN as ABCDE1234F."), Display(Name = "PAN")]
    public string? Pan { get; set; }
    [StringLength(12), RegularExpression(Patterns.Aadhaar, ErrorMessage = "Enter the 12-digit Aadhaar number."), Display(Name = "Aadhaar")]
    public string? Aadhaar { get; set; }

    [StringLength(150), Display(Name = "Account holder name")] public string? BankAccountHolder { get; set; }
    [StringLength(150), Display(Name = "Bank name")] public string? BankName { get; set; }
    [StringLength(20), RegularExpression("^[0-9]{9,18}$", ErrorMessage = "Enter 9 to 18 digits."), Display(Name = "Account number")]
    public string? BankAccountNumber { get; set; }
    [StringLength(11), RegularExpression("^[A-Za-z]{4}0[A-Za-z0-9]{6}$", ErrorMessage = "Enter the IFSC as SBIN0001234."), Display(Name = "IFSC")]
    public string? BankIfsc { get; set; }

    public static MyProfileInput From(Employee e) => new()
    {
        Gender = e.Gender, DateOfBirth = e.DateOfBirth, Mobile = e.Mobile, Email = e.Email, HighestQualification = e.HighestQualification,
        CurrentAddress = e.CurrentAddress, EmergencyContactName = e.EmergencyContactName, EmergencyContactPhone = e.EmergencyContactPhone,
        BankAccountHolder = e.BankAccountHolder, BankName = e.BankName, BankAccountNumber = e.BankAccountNumber, BankIfsc = e.BankIfsc,
    };

    public void ApplyTo(Employee e)
    {
        static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        e.Gender = Gender;
        e.DateOfBirth = DateOfBirth;
        e.Mobile = Clean(Mobile);
        e.Email = Clean(Email);
        e.HighestQualification = Clean(HighestQualification);
        e.CurrentAddress = Clean(CurrentAddress);
        e.EmergencyContactName = Clean(EmergencyContactName);
        e.EmergencyContactPhone = Clean(EmergencyContactPhone);
        if (Clean(Pan) is { } pan) e.Pan = pan.ToUpperInvariant();
        if (Clean(Aadhaar) is { } aadhaar) e.Aadhaar = aadhaar;
        e.BankAccountHolder = Clean(BankAccountHolder);
        e.BankName = Clean(BankName);
        e.BankAccountNumber = Clean(BankAccountNumber);
        e.BankIfsc = Clean(BankIfsc)?.ToUpperInvariant();
    }
}

public class ExperienceInput
{
    [Required, StringLength(200)] public string Company { get; set; } = string.Empty;
    [StringLength(100)] public string? Designation { get; set; }
    [Required, Display(Name = "From")] public DateOnly? FromDate { get; set; }
    [Required, Display(Name = "To")] public DateOnly? ToDate { get; set; }
}

public class ResignationInput
{
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required, Display(Name = "Last working day you are asking for")] public DateOnly? RequestedLastDay { get; set; }
}

public class SalaryRevisionInput
{
    [Required, Display(Name = "Effective from")] public DateOnly? EffectiveFrom { get; set; }
    [Required, Range(1, 10_000_000), Display(Name = "New monthly salary (₹)")] public decimal? NewSalary { get; set; }
    [StringLength(500)] public string? Remarks { get; set; }
}

/// <summary>Masks for showing identifiers: XXXX-XXXX-1234, ABXXXXX34F, XXXXXX6789.</summary>
public static class Masks
{
    public static string? Aadhaar(string? v) => v is { Length: 12 } ? $"XXXX-XXXX-{v[8..]}" : v;
    public static string? Pan(string? v) => v is { Length: 10 } ? $"{v[..2]}XXXXX{v[7..]}" : v;
    public static string? Account(string? v) => v is { Length: > 4 } ? new string('X', v.Length - 4) + v[^4..] : v;
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
    [StringLength(150), Display(Name = "Highest qualification")] public string? HighestQualification { get; set; }
    [StringLength(500), Display(Name = "Current address")] public string? CurrentAddress { get; set; }
    [StringLength(150), Display(Name = "Emergency contact name")] public string? EmergencyContactName { get; set; }
    [Phone, StringLength(20), Display(Name = "Emergency contact phone")] public string? EmergencyContactPhone { get; set; }

    [StringLength(100)] public string? Department { get; set; }
    [StringLength(100)] public string? Designation { get; set; }
    [Display(Name = "Reporting manager")] public int? ReportingManagerId { get; set; }
    [Display(Name = "Employment type")] public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    [Required, Display(Name = "Date of joining")] public DateOnly? DateOfJoining { get; set; }
    [Display(Name = "Probation ends on")] public DateOnly? ProbationEndsOn { get; set; }
    [Display(Name = "Date of leaving")] public DateOnly? DateOfLeaving { get; set; }
    [Display(Name = "Shift")] public int? ShiftId { get; set; }
    [StringLength(20), RegularExpression("^[A-Za-z0-9-]*$", ErrorMessage = "Letters, digits and dashes only."), Display(Name = "Biometric ID")]
    public string? AttendanceId { get; set; }
    [Range(0, 10_000_000), Display(Name = "Monthly salary (₹)")] public decimal? MonthlySalary { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    [Range(0, 180), Display(Name = "Notice period (days)")] public int NoticePeriodDays { get; set; } = 30;

    // IDs and bank: PAN and Aadhaar are shown masked; leaving them blank keeps what is saved.
    [StringLength(10), RegularExpression("^[A-Za-z]{5}[0-9]{4}[A-Za-z]$", ErrorMessage = "Enter the PAN as ABCDE1234F."), Display(Name = "PAN")]
    public string? Pan { get; set; }
    [StringLength(12), RegularExpression(Patterns.Aadhaar, ErrorMessage = "Enter the 12-digit Aadhaar number."), Display(Name = "Aadhaar")]
    public string? Aadhaar { get; set; }
    [StringLength(150), Display(Name = "Account holder name")] public string? BankAccountHolder { get; set; }
    [StringLength(150), Display(Name = "Bank name")] public string? BankName { get; set; }
    [StringLength(20), RegularExpression("^[0-9]{9,18}$", ErrorMessage = "Enter 9 to 18 digits."), Display(Name = "Account number")]
    public string? BankAccountNumber { get; set; }
    [StringLength(11), RegularExpression("^[A-Za-z]{4}0[A-Za-z0-9]{6}$", ErrorMessage = "Enter the IFSC as SBIN0001234."), Display(Name = "IFSC")]
    public string? BankIfsc { get; set; }

    // New employees only: create their login in the same step.
    [Display(Name = "Give login access now")] public bool CreateLogin { get; set; }
    public string LoginRole { get; set; } = AppRoles.Employee;

    public static EmployeeInput From(Employee e) => new()
    {
        Id = e.UniqueId, EmpCode = e.EmpCode, FirstName = e.FirstName, LastName = e.LastName, Gender = e.Gender,
        DateOfBirth = e.DateOfBirth, Email = e.Email, Mobile = e.Mobile, HighestQualification = e.HighestQualification,
        CurrentAddress = e.CurrentAddress, EmergencyContactName = e.EmergencyContactName, EmergencyContactPhone = e.EmergencyContactPhone,
        Department = e.Department, Designation = e.Designation, ReportingManagerId = e.ReportingManagerId, EmploymentType = e.EmploymentType,
        DateOfJoining = e.DateOfJoining, ProbationEndsOn = e.ProbationEndsOn, DateOfLeaving = e.DateOfLeaving, ShiftId = e.ShiftId,
        AttendanceId = e.AttendanceId, MonthlySalary = e.MonthlySalary, Status = e.Status, NoticePeriodDays = e.NoticePeriodDays,
        BankAccountHolder = e.BankAccountHolder, BankName = e.BankName, BankAccountNumber = e.BankAccountNumber, BankIfsc = e.BankIfsc,
    };

    public void ApplyTo(Employee e)
    {
        static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        e.FirstName = FirstName.Trim();
        e.LastName = Clean(LastName);
        e.Gender = Gender;
        e.DateOfBirth = DateOfBirth;
        e.Email = Clean(Email);
        e.Mobile = Clean(Mobile);
        e.HighestQualification = Clean(HighestQualification);
        e.CurrentAddress = Clean(CurrentAddress);
        e.EmergencyContactName = Clean(EmergencyContactName);
        e.EmergencyContactPhone = Clean(EmergencyContactPhone);
        e.Department = Clean(Department);
        e.Designation = Clean(Designation);
        e.ReportingManagerId = ReportingManagerId;
        e.EmploymentType = EmploymentType;
        e.DateOfJoining = DateOfJoining!.Value;
        e.ProbationEndsOn = ProbationEndsOn;
        e.DateOfLeaving = DateOfLeaving;
        e.ShiftId = ShiftId;
        // Blank keeps the current ID (a new employee gets their employee code).
        if (Clean(AttendanceId) is { } biometric) e.AttendanceId = biometric.ToUpperInvariant();
        e.MonthlySalary = MonthlySalary;
        e.Status = Status;
        e.NoticePeriodDays = NoticePeriodDays;
        if (Clean(Pan) is { } pan) e.Pan = pan.ToUpperInvariant();
        if (Clean(Aadhaar) is { } aadhaar) e.Aadhaar = aadhaar;
        e.BankAccountHolder = Clean(BankAccountHolder);
        e.BankName = Clean(BankName);
        e.BankAccountNumber = Clean(BankAccountNumber);
        e.BankIfsc = Clean(BankIfsc)?.ToUpperInvariant();
    }
}
