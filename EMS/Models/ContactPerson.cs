using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>Primary/secondary contact, help desk, director, manager etc. for head office (BranchId null) or a branch.</summary>
public class ContactPerson : AuditableEntity
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public ContactRole Role { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(100)] public string? Designation { get; set; }
    [Required, Phone, StringLength(20)] public string Phone { get; set; } = string.Empty;
    [Phone, StringLength(20)] public string? AlternatePhone { get; set; }
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
}
