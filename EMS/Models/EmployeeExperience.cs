using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>A job the employee held before joining; their total prior experience is the sum of these.</summary>
public class EmployeeExperience : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    [Required, StringLength(200)] public string Company { get; set; } = string.Empty;
    [StringLength(100)] public string? Designation { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    public int Months => Math.Max(0, (ToDate.Year - FromDate.Year) * 12 + ToDate.Month - FromDate.Month + (ToDate.Day >= FromDate.Day ? 0 : -1));
}
