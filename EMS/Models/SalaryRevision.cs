using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>A change to an employee's monthly salary (appraisal). Each one has an appraisal letter.</summary>
public class SalaryRevision : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateOnly EffectiveFrom { get; set; }
    public decimal PreviousSalary { get; set; }
    public decimal NewSalary { get; set; }
    [StringLength(500)] public string? Remarks { get; set; }

    public decimal IncreasePercent => PreviousSalary == 0 ? 0 : Math.Round((NewSalary - PreviousSalary) * 100 / PreviousSalary, 1);
}
