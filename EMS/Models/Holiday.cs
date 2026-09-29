using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>Yearly holiday plan. Only used when Organization.IsHolidayCalendarApplicable is true.</summary>
public class Holiday : AuditableEntity
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    /// <summary>Null = applies to all branches; set for state-specific holidays.</summary>
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public DateOnly Date { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;

    /// <summary>Restricted/optional holiday the employee may choose to take.</summary>
    public bool IsOptional { get; set; }
}
