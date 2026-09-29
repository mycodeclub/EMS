using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>Configurable per organization: EL/PL, CL, SL/Medical, Maternity, Comp-off, LOP ...</summary>
public class LeaveType : AuditableEntity
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    [Required, StringLength(10)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;

    /// <summary>Default days allocated per year.</summary>
    [Range(0, 365)] public decimal AnnualQuota { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool AllowCarryForward { get; set; }
    [Range(0, 365)] public decimal? MaxCarryForward { get; set; }
    public bool IsActive { get; set; } = true;
}
