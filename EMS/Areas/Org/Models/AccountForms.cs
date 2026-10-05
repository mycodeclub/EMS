using System.ComponentModel.DataAnnotations;

namespace EMS.Areas.Org.Models;

/// <summary>Forms on the signed-in user's Account &amp; password page.</summary>
public class ChangePasswordForm
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")] public string CurrentPassword { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "New password")] public string NewPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match."), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangeEmailForm
{
    [Required, EmailAddress, StringLength(150), Display(Name = "New sign-in email")] public string NewEmail { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Display(Name = "Current password")] public string CurrentPassword { get; set; } = string.Empty;
}
