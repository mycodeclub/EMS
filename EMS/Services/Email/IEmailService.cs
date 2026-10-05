namespace EMS.Services.Email;

/// <summary>Sends the app's emails. Never throws for a delivery problem: it logs it and returns false, so the caller can tell the user.</summary>
public interface IEmailService
{
    /// <summary>True when the message was handed to the mail server (or saved to the pickup folder).</summary>
    Task<bool> TrySendAsync(EmailMessage email);
}
