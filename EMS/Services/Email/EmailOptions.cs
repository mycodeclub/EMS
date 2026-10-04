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

    /// <summary>587 (STARTTLS) or 465 (SSL from the start); 25 for plain relays.</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Encrypt the connection: SSL on port 465, STARTTLS on other ports.</summary>
    public bool EnableSsl { get; set; } = true;

    public string? UserName { get; set; }
    public string? Password { get; set; }

    /// <summary>Relative to the app's content root.</summary>
    public string PickupDirectory { get; set; } = "App_Data/mail";
}
