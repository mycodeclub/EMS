using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>
/// History of which biometric (attendance) ID was held by which employee and when.
/// Maintained automatically by ApplicationDbContext when an employee is added, their AttendanceId changes,
/// DateOfLeaving is set, or the employee is deleted. An ID can be reused by another employee only after
/// the previous holder's tenure has ended; tenures of the same ID never overlap.
/// </summary>
public class BiometricIdAssignment : AuditableEntity
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    [Required, StringLength(20)] public string AttendanceId { get; set; } = string.Empty;

    /// <summary>Tenure, both dates inclusive. ValidTo = null means currently assigned.</summary>
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    [NotMapped] public bool IsCurrent => ValidTo is null;
}

public static class BiometricIdAssignmentQueries
{
    /// <summary>The assignment (hence the employee) that held <paramref name="attendanceId"/> on <paramref name="date"/>. Used when processing punches.</summary>
    public static IQueryable<BiometricIdAssignment> HeldOn(this IQueryable<BiometricIdAssignment> query, int organizationId, string attendanceId, DateOnly date) =>
        query.Where(a => a.OrganizationId == organizationId && a.AttendanceId == attendanceId
                         && a.ValidFrom <= date && (a.ValidTo == null || a.ValidTo >= date));
}
