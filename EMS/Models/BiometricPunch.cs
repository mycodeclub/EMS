using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>
/// Raw punch as synced from the biometric device, kept unchanged for audit/re-processing.
/// AttendanceId is what the device knows; EmployeeId is resolved from the BiometricIdAssignment valid on the punch date.
/// </summary>
public class BiometricPunch : ISoftDelete
{
    [Key] public long UniqueId { get; set; }

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    [StringLength(50)] public string? DeviceId { get; set; }
    [Required, StringLength(20)] public string AttendanceId { get; set; } = string.Empty;
    /// <summary>Local (device) time.</summary>
    public DateTime PunchTime { get; set; }

    /// <summary>Null when no employee held this AttendanceId on the punch date.</summary>
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public bool IsProcessed { get; set; }
    public DateTime SyncedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    [StringLength(450)] public string? DeletedBy { get; set; }
}
