namespace EMS.Services.Email;

/// <summary>One outgoing email. <see cref="HtmlBody"/> is a complete HTML document (see <see cref="EmailTemplates"/>).</summary>
public record EmailMessage(string To, string Subject, string HtmlBody);
