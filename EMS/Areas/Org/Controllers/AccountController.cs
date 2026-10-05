using EMS.Areas.Org.Models;
using EMS.Services.Auth;
using EMS.Services.Demo;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EMS.Areas.Org.Controllers;

/// <summary>
/// The signed-in user's own login: change sign-in email and password. Not available to the shared demo logins, nor to a
/// super admin viewing a customer's panel (that would change the customer's login).
/// </summary>
public class AccountController(OrganizationContext context, UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn) : OrgController(context)
{
    public async Task<IActionResult> Index()
    {
        ViewData["Locked"] = LockedReason();
        ViewData["Email"] = (await users.GetUserAsync(User))?.Email;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Password([Bind(Prefix = "Password")] ChangePasswordForm form)
    {
        if (LockedReason() is { } locked) return Refuse(locked);
        if (!ModelState.IsValid) return Refuse(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
        if (await users.GetUserAsync(User) is not { } user) return Challenge();

        var result = await users.ChangePasswordAsync(user, form.CurrentPassword, form.NewPassword);
        if (!result.Succeeded) return Refuse(string.Join(" ", result.Errors.Select(e => e.Description)));
        await signIn.RefreshSignInAsync(user);
        TempData["Message"] = "Password changed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Email([Bind(Prefix = "Email")] ChangeEmailForm form)
    {
        if (LockedReason() is { } locked) return Refuse(locked);
        if (!ModelState.IsValid) return Refuse(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
        if (await users.GetUserAsync(User) is not { } user) return Challenge();
        if (!await users.CheckPasswordAsync(user, form.CurrentPassword)) return Refuse("The current password is not right.");

        var email = form.NewEmail.Trim();
        if (await users.FindByEmailAsync(email) is { } other && other.Id != user.Id) return Refuse($"{email} is already used by another login.");

        // Sign-in email and user name are the same, as everywhere in EMS.
        var token = await users.GenerateChangeEmailTokenAsync(user, email);
        var result = await users.ChangeEmailAsync(user, email, token);
        if (result.Succeeded) result = await users.SetUserNameAsync(user, email);
        if (!result.Succeeded) return Refuse(string.Join(" ", result.Errors.Select(e => e.Description)));

        await signIn.RefreshSignInAsync(user);
        TempData["Message"] = $"Sign-in email changed to {email}. Use it next time you sign in.";
        return RedirectToAction(nameof(Index));
    }

    private string? LockedReason() =>
        User.IsImpersonated() ? "You are viewing this panel as a super admin, so this login's email and password cannot be changed here."
        : User.HasClaim(c => c.Type == DemoSeeder.DemoClaim) ? "This is a shared demo login, so its email and password cannot be changed. In your own EMS account this page lets you change both."
        : null;

    private RedirectToActionResult Refuse(string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
