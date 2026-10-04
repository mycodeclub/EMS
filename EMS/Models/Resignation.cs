using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>
/// An employee's resignation. When HR accepts it, the last working day is fixed, the employee goes on notice
/// and their leaving date is set.
/// </summary>
public class Resignation : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;

    /// <summary>The last day the employee asked for; by default the end of their notice period.</summary>
    public DateOnly RequestedLastDay { get; set; }

    /// <summary>Set when accepted; may differ from the requested day (notice shortened or extended).</summary>
    public DateOnly? LastWorkingDay { get; set; }

    public ResignationStatus Status { get; set; } = ResignationStatus.Pending;
    public DateTime? ActionedAt { get; set; }
    [StringLength(500)] public string? Remarks { get; set; }
}
