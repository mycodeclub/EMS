using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

public class Organization : AuditableEntity
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(250)] public string? RegisteredName { get; set; }

    [StringLength(15), RegularExpression(Patterns.Gstin, ErrorMessage = "Invalid GSTIN.")]
    public string? GstNumber { get; set; }

    /// <summary>Head office address.</summary>
    public Address Address { get; set; } = new();

    [StringLength(500)] public string? LogoPath { get; set; }
    [Phone, StringLength(20)] public string? Phone { get; set; }
    [EmailAddress, StringLength(150)] public string? Email { get; set; }

    /// <summary>Set by super admin. Off for 24x7 businesses (hospital, hotel) that work on rosters instead of a holiday list.</summary>
    public bool IsHolidayCalendarApplicable { get; set; } = true;

    public bool IsActive { get; set; } = true;

    /// <summary>Head-office contacts have BranchId = null; branch POCs carry their BranchId.</summary>
    public ICollection<ContactPerson> Contacts { get; set; } = [];
    public ICollection<Branch> Branches { get; set; } = [];
    public ICollection<Shift> Shifts { get; set; } = [];
    public ICollection<Employee> Employees { get; set; } = [];
    public ICollection<LeaveType> LeaveTypes { get; set; } = [];
    public ICollection<Holiday> Holidays { get; set; } = [];
}
