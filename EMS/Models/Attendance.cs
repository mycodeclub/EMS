using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>Processed daily attendance, built from BiometricPunch rows (or entered manually).</summary>
public class Attendance : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    /// <summary>Date the shift started. A 22:00-06:00 shift logged out on the 2nd still belongs to the 1st.</summary>
    public DateOnly AttendanceDate { get; set; }

    /// <summary>Local (device) time.</summary>
    public DateTime? LoginAt { get; set; }
    public DateTime? LogoffAt { get; set; }
    public int? WorkedMinutes { get; set; }

    public AttendanceStatus Status { get; set; }
    public bool IsLate { get; set; }
    public AttendanceSource Source { get; set; } = AttendanceSource.Biometric;
    [StringLength(500)] public string? Remarks { get; set; }
}
