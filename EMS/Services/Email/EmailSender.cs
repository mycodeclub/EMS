using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EMS.Services.Email;

/// <summary>Sends HTML mail over SMTP with MailKit, or saves it as an .eml file when no SMTP host is configured.</summary>
public class EmailSender(IOptions<EmailOptions> options, IWebHostEnvironment env, ILogger<EmailSender> logger) : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var settings = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlMessage }.ToMessageBody();

        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            var folder = Path.Combine(env.ContentRootPath, settings.PickupDirectory);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");
            await message.WriteToAsync(path);
            logger.LogInformation("Email \"{Subject}\" to {To} saved to {Path} (no SMTP host configured).", subject, email, path);
            return;
        }

        // Port 465 is SSL from the first byte; other ports upgrade with STARTTLS when encryption is on.
        var security = !settings.EnableSsl ? SecureSocketOptions.None
            : settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        using var client = new SmtpClient { Timeout = 30_000 };
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, security);
        if (!string.IsNullOrEmpty(settings.UserName))
            await client.AuthenticateAsync(settings.UserName, settings.Password ?? string.Empty);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
        logger.LogInformation("Email \"{Subject}\" sent to {To}.", subject, email);
    }
}
