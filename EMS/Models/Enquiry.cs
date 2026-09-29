using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models;

/// <summary>Free-trial / demo / contact request submitted from the public landing page.</summary>
public class Enquiry : AuditableEntity
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Phone { get; set; } = string.Empty;
    [Required, StringLength(200)] public string OrganizationName { get; set; } = string.Empty;
    public Industry Industry { get; set; }
    public TeamSize TeamSize { get; set; }
    public EnquiryInterest Interest { get; set; }
    [StringLength(2000)] public string? Message { get; set; }

    public EnquiryStatus Status { get; set; } = EnquiryStatus.New;
    /// <summary>Internal follow-up notes by the sales team.</summary>
    [StringLength(2000)] public string? Notes { get; set; }
}
