using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace EMS.Services.Email;

/// <summary>Sends HTML mail over SMTP, or saves it as an .eml file when no SMTP host is configured.</summary>
public class EmailSender(IOptions<EmailOptions> options, IWebHostEnvironment env, ILogger<EmailSender> logger) : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var settings = options.Value;
        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true,
        };
        message.To.Add(email);

        using var client = new SmtpClient();
        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            var folder = Path.Combine(env.ContentRootPath, settings.PickupDirectory);
            Directory.CreateDirectory(folder);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = folder;
            await client.SendMailAsync(message);
            logger.LogInformation("Email \"{Subject}\" to {To} saved to {Folder} (no SMTP host configured).", subject, email, folder);
            return;
        }

        client.Host = settings.SmtpHost;
        client.Port = settings.SmtpPort;
        client.EnableSsl = settings.EnableSsl;
        if (!string.IsNullOrEmpty(settings.UserName))
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);

        await client.SendMailAsync(message);
        logger.LogInformation("Email \"{Subject}\" sent to {To}.", subject, email);
    }
}
