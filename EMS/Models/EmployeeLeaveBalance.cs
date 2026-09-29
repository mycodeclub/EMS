using System.ComponentModel.DataAnnotations.Schema;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>One row per employee + leave type + year. Used is updated when a leave application is approved/cancelled.</summary>
public class EmployeeLeaveBalance : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;

    public int Year { get; set; }

    public decimal Allocated { get; set; }
    public decimal CarriedForward { get; set; }
    public decimal Used { get; set; }

    [NotMapped] public decimal Total => Allocated + CarriedForward;
    [NotMapped] public decimal Available => Total - Used;
}
