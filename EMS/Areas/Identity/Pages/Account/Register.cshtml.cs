using System.ComponentModel.DataAnnotations;
using System.Text;
using EMS.Services.Email;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace EMS.Areas.Identity.Pages.Account;

/// <summary>
/// Self sign-up. The account must confirm its email before it can sign in, so a confirmation link is emailed. If that
/// email cannot be sent the account is still created, and "Didn't get the confirmation email?" on the sign-in page resends it.
/// </summary>
public class RegisterModel(
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn,
    IEmailService email,
    EmailTemplates templates,
    ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public IList<AuthenticationScheme> ExternalLogins { get; set; } = [];

    public class InputModel
    {
        [Required, EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password), Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password), Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        ExternalLogins = (await signIn.GetExternalAuthenticationSchemesAsync()).ToList();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ExternalLogins = (await signIn.GetExternalAuthenticationSchemesAsync()).ToList();
        if (!ModelState.IsValid) return Page();

        var address = Input.Email.Trim();
        var user = new IdentityUser { UserName = address, Email = address };
        var result = await users.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }
        logger.LogInformation("User {Email} created a new account with password.", address);

        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GenerateEmailConfirmationTokenAsync(user)));
        var confirmUrl = Url.Page("/Account/ConfirmEmail", pageHandler: null,
            values: new { area = "Identity", userId = user.Id, code, returnUrl }, protocol: Request.Scheme)!;
        await email.TrySendAsync(templates.ConfirmEmail(address, confirmUrl));

        if (users.Options.SignIn.RequireConfirmedAccount)
            return RedirectToPage("RegisterConfirmation", new { email = address, returnUrl });

        await signIn.SignInAsync(user, isPersistent: false);
        return LocalRedirect(returnUrl);
    }
}
