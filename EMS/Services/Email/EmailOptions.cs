namespace EMS.Services.Email;

/// <summary>
/// Outgoing mail ("Email" section of appsettings). With no SmtpHost the messages are written as .eml files
/// to PickupDirectory instead of being sent, which is the default for local development.
/// </summary>
public class EmailOptions
{
    public const string Section = "Email";

    public string FromAddress { get; set; } = "no-reply@ems.local";
    public string FromName { get; set; } = "EMS";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }

    /// <summary>Relative to the app's content root.</summary>
    public string PickupDirectory { get; set; } = "App_Data/mail";
}
