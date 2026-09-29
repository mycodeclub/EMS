using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

public class Branch : AuditableEntity
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Code { get; set; } = string.Empty;

    public Address Address { get; set; } = new();

    /// <summary>GST registration is state-wise; a branch in another state has its own GSTIN. Null = use organization's.</summary>
    [StringLength(15), RegularExpression(Patterns.Gstin, ErrorMessage = "Invalid GSTIN.")]
    public string? GstNumber { get; set; }

    [Phone, StringLength(20)] public string? Phone { get; set; }
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ContactPerson> Contacts { get; set; } = [];
    public ICollection<Employee> Employees { get; set; } = [];
}
