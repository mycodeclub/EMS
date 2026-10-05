using System.Net;
using EMS.Models;
using EMS.Models.Common;
using EMS.Models.Landing;
using Microsoft.Extensions.Options;

namespace EMS.Services.Email;

/// <summary>
/// Every email EMS sends, in one branded layout: inline styles and tables, which is what email clients render reliably.
/// Values are HTML-encoded here, so callers pass plain text.
/// </summary>
public class EmailTemplates(IOptions<CompanyOptions> company)
{
    private string Product => company.Value.ProductName;
    private string Company => company.Value.Name;

    /// <summary>Super admin offered a free trial: the owner's sign-in details and what setup involves.</summary>
    public EmailMessage TrialWelcome(string to, string ownerName, Organization organization, string temporaryPassword, string loginUrl) => new(
        to,
        $"Your {Product} free trial is ready — sign in to set up {organization.Name}",
        Layout($"Welcome, {E(ownerName)}",
            $"""
            <p style="margin:0 0 16px;line-height:1.5;">Your free trial for <b>{E(organization.Name)}</b> is ready. It runs until <b>{organization.TrialEndsOn?.ToShortDate()}</b>.</p>
            {Credentials(to, temporaryPassword)}
            {Button(loginUrl, $"Sign in to {Product}")}
            <p style="margin:0 0 8px;font-weight:600;">After you sign in, we'll walk you through:</p>
            <ol style="margin:0 0 20px;padding-left:20px;line-height:1.7;">
              <li>Choosing your own password (the temporary one stops working)</li>
              <li>Completing your organization profile</li>
              <li>Setting up your shifts — one general shift, or several for 24x7 operations</li>
              <li>Adding your employees</li>
            </ol>
            """,
            $"Didn't expect this email? You can ignore it. Questions? Just reply and the {E(Company)} team will help."));

    /// <summary>HR gave an employee a login: their sign-in details and the joining checklist to complete.</summary>
    public EmailMessage EmployeeLogin(string to, string firstName, string organizationName, string temporaryPassword, string loginUrl) => new(
        to,
        $"Your {organizationName} login for {Product}",
        Layout($"Hello {E(firstName)}",
            $"""
            <p style="margin:0 0 16px;line-height:1.5;">Welcome to <b>{E(organizationName)}</b>! You can now sign in to {E(Product)}.</p>
            {Credentials(to, temporaryPassword)}
            {Button(loginUrl, "Sign in")}
            <p style="margin:0 0 8px;font-weight:600;">After you sign in:</p>
            <ol style="margin:0 0 20px;padding-left:20px;line-height:1.7;">
              <li>Change your password under <b>Account &amp; password</b></li>
              <li>Complete your <b>Joining checklist</b>: your details, PAN, Aadhaar, bank account, photo and joining documents</li>
            </ol>
            """,
            $"This login was created by {E(organizationName)}'s HR team. If you were not expecting it, contact them."));

    /// <summary>A new account confirms its email address before it can sign in.</summary>
    public EmailMessage ConfirmEmail(string to, string confirmUrl) => new(
        to,
        $"Confirm your email for {Product}",
        Layout("Confirm your email",
            $"""
            <p style="margin:0 0 20px;line-height:1.5;">Thanks for creating a {E(Product)} account. Confirm that <b>{E(to)}</b> is your email address to finish signing up.</p>
            {Button(confirmUrl, "Confirm email")}
            <p style="margin:0 0 8px;font-size:13px;color:#6e6e73;line-height:1.5;">Or paste this link into your browser:<br /><span style="word-break:break-all;">{E(confirmUrl)}</span></p>
            """,
            "Didn't create an account? You can ignore this email."));

    /// <summary>A short message from ASP.NET Core Identity's built-in pages (already HTML), e.g. a password-reset link.</summary>
    public EmailMessage Notice(string to, string subject, string htmlMessage) => new(
        to,
        subject,
        Layout(E(subject),
            $"""<p style="margin:0 0 20px;line-height:1.5;">{htmlMessage}</p>""",
            "Didn't ask for this? You can ignore this email; nothing changes on your account."));

    private string Layout(string headingHtml, string bodyHtml, string footerHtml) =>
        $"""
        <!DOCTYPE html>
        <html><body style="margin:0;padding:24px;background:#f5f5f7;font-family:-apple-system,'Segoe UI',Roboto,Arial,sans-serif;color:#1d1d1f;">
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center">
        <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:16px;padding:32px;">
          <tr><td>
            <p style="margin:0 0 4px;font-size:13px;color:#0071e3;font-weight:600;">{E(Product)} by {E(Company)}</p>
            <h1 style="margin:0 0 16px;font-size:24px;">{headingHtml}</h1>
            {bodyHtml}
            <p style="margin:0;font-size:13px;color:#6e6e73;line-height:1.5;">{footerHtml}</p>
          </td></tr>
        </table>
        </td></tr></table>
        </body></html>
        """;

    private static string Credentials(string email, string password) =>
        $"""
        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 20px;background:#f5f5f7;border-radius:12px;width:100%;">
          <tr><td style="padding:16px 20px;font-size:15px;line-height:1.7;">
            Sign-in email: <b>{E(email)}</b><br />
            Temporary password: <b style="font-family:Consolas,Menlo,monospace;">{E(password)}</b>
          </td></tr>
        </table>
        """;

    private static string Button(string url, string label) =>
        $"""<p style="margin:0 0 24px;"><a href="{E(url)}" style="display:inline-block;padding:12px 22px;background:#0071e3;color:#ffffff;border-radius:980px;text-decoration:none;font-weight:600;">{E(label)}</a></p>""";

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
