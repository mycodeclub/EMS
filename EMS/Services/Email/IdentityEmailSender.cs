using Microsoft.AspNetCore.Identity.UI.Services;

namespace EMS.Services.Email;

/// <summary>
/// The sender ASP.NET Core Identity's built-in pages use (forgot password, resend confirmation, change email). Wraps their
/// short messages in the EMS email layout. A delivery failure is logged rather than thrown, so those pages still load.
/// </summary>
public class IdentityEmailSender(IEmailService sender, EmailTemplates templates) : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage) =>
        sender.TrySendAsync(templates.Notice(email, subject, htmlMessage));
}
