using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMS.Models.Common;
using Microsoft.AspNetCore.Identity;

namespace EMS.Models;

public class Employee : AuditableEntity
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    /// <summary>Default shift the employee was hired for. Null = no fixed shift (general/rotational).</summary>
    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public int? ReportingManagerId { get; set; }
    public Employee? ReportingManager { get; set; }

    /// <summary>Login account for employee self-service (optional).</summary>
    public string? UserId { get; set; }
    public IdentityUser? User { get; set; }

    [Required, StringLength(20), Display(Name = "Employee Code")]
    public string EmpCode { get; set; } = string.Empty;

    /// <summary>Current (or last) enrolment ID on the biometric device. Defaults to EmpCode when left blank.
    /// Full history with tenures is in BiometricIdHistory.</summary>
    [StringLength(20), Display(Name = "Attendance (Biometric) ID")]
    public string AttendanceId { get; set; } = string.Empty;

    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [StringLength(100)] public string? MiddleName { get; set; }
    [StringLength(100)] public string? LastName { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    [Phone, StringLength(20)] public string? Mobile { get; set; }

    [StringLength(10), RegularExpression(Patterns.Pan, ErrorMessage = "Invalid PAN.")]
    public string? Pan { get; set; }

    /// <summary>Sensitive (Aadhaar Act): encrypt at rest and show masked (XXXX-XXXX-1234) in UI.</summary>
    [StringLength(12), RegularExpression(Patterns.Aadhaar, ErrorMessage = "Invalid Aadhaar number.")]
    public string? Aadhaar { get; set; }

    [StringLength(500)] public string? PhotoPath { get; set; }
    [StringLength(150)] public string? HighestQualification { get; set; }
    [StringLength(100)] public string? Department { get; set; }
    [StringLength(100)] public string? Designation { get; set; }

    public DateOnly DateOfJoining { get; set; }
    public DateOnly? DateOfLeaving { get; set; }

    /// <summary>Experience before joining this organization.</summary>
    [Range(0, 720)] public int PriorExperienceMonths { get; set; }

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    /// <summary>Gross monthly salary in rupees; salary slips pro-rate it by paid days.</summary>
    [Range(0, 10_000_000), Display(Name = "Monthly salary")]
    public decimal? MonthlySalary { get; set; }

    [NotMapped]
    public string FullName => string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(n => !string.IsNullOrWhiteSpace(n)));

    /// <summary>Prior experience + tenure here, in months.</summary>
    public int TotalExperienceMonths(DateOnly asOf)
    {
        var end = DateOfLeaving is { } left && left < asOf ? left : asOf;
        var tenure = (end.Year - DateOfJoining.Year) * 12 + end.Month - DateOfJoining.Month - (end.Day < DateOfJoining.Day ? 1 : 0);
        return PriorExperienceMonths + Math.Max(0, tenure);
    }

    public ICollection<EmployeeLeaveBalance> LeaveBalances { get; set; } = [];
    public ICollection<LeaveApplication> LeaveApplications { get; set; } = [];
    public ICollection<Attendance> Attendances { get; set; } = [];
    public ICollection<BiometricIdAssignment> BiometricIdHistory { get; set; } = [];
}
