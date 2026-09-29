using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>Shift master.</summary>
public class Shift : AuditableEntity
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    /// <summary>Null = available to all branches.</summary>
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Code { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    [Range(0, 240)] public int BreakMinutes { get; set; }
    /// <summary>Check-in after StartTime + grace is marked late.</summary>
    [Range(0, 240)] public int GraceMinutes { get; set; }
    /// <summary>Minimum worked minutes for a half day / full day.</summary>
    [Range(0, 1440)] public int HalfDayMinutes { get; set; }
    [Range(0, 1440)] public int FullDayMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Night shift (e.g. 22:00-06:00). Attendance belongs to the date the shift starts.</summary>
    [NotMapped] public bool CrossesMidnight => EndTime <= StartTime;
}
