using System.ComponentModel.DataAnnotations;

namespace EMS.Models.Landing;

/// <summary>Public enquiry form. Separate from the Enquiry entity so visitors can only post these fields.</summary>
public class EnquiryInput
{
    [Required(ErrorMessage = "Please enter your name."), StringLength(150), Display(Name = "Full name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your work email."), EmailAddress(ErrorMessage = "Please enter a valid email address."), StringLength(150), Display(Name = "Work email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your phone number."), StringLength(20),
     RegularExpression(@"^\+?[0-9][0-9 ()-]{6,18}$", ErrorMessage = "Please enter a valid phone number."), Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your organization's name."), StringLength(200), Display(Name = "Organization")]
    public string OrganizationName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose your industry."), Display(Name = "Industry")]
    public Industry? Industry { get; set; }

    [Required(ErrorMessage = "Please choose your team size."), Display(Name = "Number of employees")]
    public TeamSize? TeamSize { get; set; }

    [Display(Name = "I'm interested in")]
    public EnquiryInterest Interest { get; set; } = EnquiryInterest.FreeTrial;

    [StringLength(2000), Display(Name = "Anything we should know? (optional)")]
    public string? Message { get; set; }

    /// <summary>Honeypot: hidden from people, filled in by bots. Submissions with a value are silently dropped.</summary>
    public string? Website { get; set; }

    public Enquiry ToEntity() => new()
    {
        Name = Name.Trim(),
        Email = Email.Trim(),
        Phone = Phone.Trim(),
        OrganizationName = OrganizationName.Trim(),
        Industry = Industry!.Value,
        TeamSize = TeamSize!.Value,
        Interest = Interest,
        Message = string.IsNullOrWhiteSpace(Message) ? null : Message.Trim(),
    };
}
