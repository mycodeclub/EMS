using System.ComponentModel.DataAnnotations;

namespace EMS.Models.Admin;

/// <summary>Super admin form: turn a lead or enquiry into a free-trial customer.</summary>
public class TrialInput
{
    [Required, StringLength(200), Display(Name = "Organization name")]
    public string OrganizationName { get; set; } = string.Empty;

    [Required, StringLength(150), Display(Name = "Owner / admin name")]
    public string OwnerName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150), Display(Name = "Owner email (their login)")]
    public string OwnerEmail { get; set; } = string.Empty;

    [Required, Phone, StringLength(20), Display(Name = "Owner phone")]
    public string OwnerPhone { get; set; } = string.Empty;

    [Required]
    public Industry? Industry { get; set; }

    [Range(7, 90), Display(Name = "Trial length (days)")]
    public int TrialDays { get; set; } = 30;
}
