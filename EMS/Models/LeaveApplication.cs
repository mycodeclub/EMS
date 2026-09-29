using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

public class LeaveApplication : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    /// <summary>Only valid for a single-day leave (FromDate == ToDate).</summary>
    public bool IsHalfDay { get; set; }

    /// <summary>Chargeable days, excluding holidays and weekly offs; calculated when applying.</summary>
    public decimal TotalDays { get; set; }

    [Required, StringLength(1000), Display(Name = "Explanation")]
    public string Reason { get; set; } = string.Empty;

    public DateTime AppliedOn { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public int? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ActionedOn { get; set; }
    [StringLength(500)] public string? ApproverRemarks { get; set; }
}
