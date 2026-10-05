using System.Net;
using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EMS.Services.Email;

/// <summary>
/// Sends mail over SMTP with MailKit, or saves it as an .eml file in the pickup folder when no SMTP host is configured
/// (the default for local development). Every message carries a plain-text part next to the HTML, which spam filters expect.
/// </summary>
public partial class SmtpEmailService(IOptions<EmailOptions> options, IWebHostEnvironment env, ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task<bool> TrySendAsync(EmailMessage email)
    {
        var settings = options.Value;
        if (!MailboxAddress.TryParse(email.To?.Trim(), out var recipient))
        {
            logger.LogWarning("Email \"{Subject}\" not sent: \"{To}\" is not an email address.", email.Subject, email.To);
            return false;
        }

        try
        {
            var message = Build(settings, recipient, email);
            if (string.IsNullOrWhiteSpace(settings.SmtpHost))
            {
                var path = await SaveToPickupAsync(settings, message);
                logger.LogInformation("Email \"{Subject}\" to {To} saved to {Path} (no SMTP host configured).", email.Subject, recipient.Address, path);
            }
            else
            {
                await SendSmtpAsync(settings, message);
                logger.LogInformation("Email \"{Subject}\" sent to {To}.", email.Subject, recipient.Address);
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send email \"{Subject}\" to {To} via {Host}:{Port}.",
                email.Subject, recipient.Address, settings.SmtpHost is { Length: > 0 } host ? host : "pickup folder", settings.SmtpPort);
            return false;
        }
    }

    private static MimeMessage Build(EmailOptions settings, MailboxAddress recipient, EmailMessage email)
    {
        var message = new MimeMessage { Subject = email.Subject };
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(recipient);
        if (!string.IsNullOrWhiteSpace(settings.ReplyToAddress))
            message.ReplyTo.Add(new MailboxAddress(settings.FromName, settings.ReplyToAddress));
        message.Body = new BodyBuilder { HtmlBody = email.HtmlBody, TextBody = ToPlainText(email.HtmlBody) }.ToMessageBody();
        return message;
    }

    private async Task<string> SaveToPickupAsync(EmailOptions settings, MimeMessage message)
    {
        var folder = Path.Combine(env.ContentRootPath, settings.PickupDirectory);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");
        await message.WriteToAsync(path);
        return path;
    }

    private static async Task SendSmtpAsync(EmailOptions settings, MimeMessage message)
    {
        // Port 465 is SSL from the first byte; other ports upgrade with STARTTLS when encryption is on.
        var security = !settings.EnableSsl ? SecureSocketOptions.None
            : settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        using var client = new SmtpClient { Timeout = 30_000 };
        await client.ConnectAsync(settings.SmtpHost!, settings.SmtpPort, security);
        if (!string.IsNullOrEmpty(settings.UserName))
            await client.AuthenticateAsync(settings.UserName, settings.Password ?? string.Empty);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    /// <summary>A readable text version of an email's HTML: links keep their address, paragraphs and list items keep their line breaks.</summary>
    public static string ToPlainText(string html)
    {
        var text = HeadOrStyle().Replace(html, "");
        text = Link().Replace(text, m => $"{m.Groups["text"].Value} ({WebUtility.HtmlDecode(m.Groups["href"].Value)})");
        text = LineBreak().Replace(text, "\n");
        text = ListItem().Replace(text, "\n- ");
        text = Tag().Replace(text, "");
        text = WebUtility.HtmlDecode(text);
        text = string.Join("\n", text.Split('\n').Select(line => Spaces().Replace(line, " ").Trim()));
        return BlankLines().Replace(text, "\n\n").Trim() + "\n";
    }

    [GeneratedRegex(@"<(head|style)\b.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex HeadOrStyle();
    [GeneratedRegex(@"<a\b[^>]*?href\s*=\s*[""'](?<href>[^""']*)[""'][^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex Link();
    [GeneratedRegex(@"<br\s*/?>|</(p|h[1-6]|tr|div|ol|ul)>", RegexOptions.IgnoreCase)] private static partial Regex LineBreak();
    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex ListItem();
    [GeneratedRegex(@"<[^>]+>")] private static partial Regex Tag();
    [GeneratedRegex(@"[ \t\r]+")] private static partial Regex Spaces();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex BlankLines();
}
