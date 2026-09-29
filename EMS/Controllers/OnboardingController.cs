using EMS.Data;
using EMS.Models;
using EMS.Models.Common;
using EMS.Models.Onboarding;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EMS.Controllers;

/// <summary>
/// First-time setup for a new customer's owner: password, organization profile, shifts, employees.
/// Steps can be revisited in order; finishing leads to the organization admin panel.
/// </summary>
[Authorize(Roles = AppRoles.OrgAdmin)]
public class OnboardingController(
    OrganizationContext context,
    SetupService setup,
    ApplicationDbContext db,
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (await context.GetAsync(ct) is not { } organization) return NoOrganization();
        return ToStep(OrganizationContext.NextStep(organization, User));
    }

    public async Task<IActionResult> Password(CancellationToken ct) =>
        await Guard(OnboardingStep.Password, ct) ?? View(new ChangePasswordInput());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Password(ChangePasswordInput input, CancellationToken ct)
    {
        if (await Guard(OnboardingStep.Password, ct) is { } redirect) return redirect;
        if (!ModelState.IsValid) return View(input);

        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();

        // The temporary password is replaced without asking for it again: the user has just signed in with it.
        var result = await users.RemovePasswordAsync(user);
        if (result.Succeeded) result = await users.AddPasswordAsync(user, input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(nameof(input.NewPassword), error.Description);
            return View(input);
        }

        var claims = (await users.GetClaimsAsync(user)).Where(c => c.Type == TrialService.MustChangePasswordClaim).ToList();
        if (claims.Count > 0) await users.RemoveClaimsAsync(user, claims);
        await signIn.RefreshSignInAsync(user);
        return RedirectToAction(nameof(Profile));
    }

    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        if (await Guard(OnboardingStep.Profile, ct) is { } redirect) return redirect;
        return View(ProfileInput.From((await context.GetAsync(ct))!));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileInput input, CancellationToken ct)
    {
        if (await Guard(OnboardingStep.Profile, ct) is { } redirect) return redirect;
        if (!ModelState.IsValid) return View(input);

        var organization = (await context.GetAsync(ct))!;
        input.ApplyTo(organization, OrganizationContext.DefaultBranch(organization));
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Shifts));
    }

    public async Task<IActionResult> Shifts(CancellationToken ct)
    {
        if (await Guard(OnboardingStep.Shifts, ct) is { } redirect) return redirect;
        var organization = (await context.GetAsync(ct))!;
        var saved = await setup.ShiftsAsync(organization.UniqueId, ct);

        var input = new ShiftsInput { OperatesInShifts = organization.OperatesInShifts };
        input.Shifts = saved.Count > 0
            ? saved.Select(s => new ShiftRow { Id = s.UniqueId, Name = s.Name, StartTime = s.StartTime, EndTime = s.EndTime, BreakMinutes = s.BreakMinutes }).ToList()
            : organization.Industry is Industry.Hospital or Industry.Hotel ? ShiftsInput.RoundTheClock() : ShiftsInput.GeneralShift();
        input.OperatesInShifts ??= saved.Count > 0 ? saved.Count > 1 : null;
        ViewData["Industry"] = organization.Industry;
        return View(input);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Shifts(ShiftsInput input, CancellationToken ct)
    {
        if (await Guard(OnboardingStep.Shifts, ct) is { } redirect) return redirect;
        var organization = (await context.GetAsync(ct))!;

        // One general shift unless the customer runs several.
        if (input.OperatesInShifts == false) input.Shifts = input.Shifts.Take(1).ToList();
        ValidateShifts(input, ModelState);
        if (!ModelState.IsValid)
        {
            ViewData["Industry"] = organization.Industry;
            return View(input);
        }

        if (await setup.SaveShiftsAsync(organization, input.OperatesInShifts!.Value, input.Shifts, ct) is { } error)
        {
            ModelState.AddModelError(string.Empty, error);
            ViewData["Industry"] = organization.Industry;
            return View(input);
        }
        return RedirectToAction(nameof(Employees));
    }

    public async Task<IActionResult> Employees(CancellationToken ct)
    {
        if (await Guard(OnboardingStep.Employees, ct) is { } redirect) return redirect;
        return await EmployeesView(new EmployeesInput { Rows = Enumerable.Range(0, 5).Select(_ => new EmployeeRow()).ToList() }, ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Employees(EmployeesInput input, string? skip, CancellationToken ct)
    {
        if (await Guard(OnboardingStep.Employees, ct) is { } redirect) return redirect;
        var organization = (await context.GetAsync(ct))!;

        if (skip is null)
        {
            for (var i = 0; i < input.Rows.Count; i++)
            {
                if (!input.Rows[i].IsBlank && input.Rows[i].DateOfJoining is null)
                    ModelState.AddModelError($"Rows[{i}].DateOfJoining", "Enter the joining date.");
            }
            if (!ModelState.IsValid) return await EmployeesView(input, ct);

            var (_, error) = await setup.AddEmployeesAsync(organization, input.Rows, ct);
            if (error is not null)
            {
                ModelState.AddModelError(string.Empty, error);
                return await EmployeesView(input, ct);
            }
        }

        organization.OnboardingCompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        TempData["Welcome"] = "true";
        return RedirectToAction("Index", "Dashboard", new { area = "Org" });
    }

    /// <summary>Shared by the onboarding wizard and the Shifts page of the admin panel.</summary>
    public static void ValidateShifts(ShiftsInput input, Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState)
    {
        if (input.Shifts.Count == 0) modelState.AddModelError(string.Empty, "Add at least one shift.");
        if (input.OperatesInShifts == true && input.Shifts.Count < 2)
            modelState.AddModelError(string.Empty, "Add each shift you run (at least two), or choose \"No\" for a single shift.");

        for (var i = 0; i < input.Shifts.Count; i++)
        {
            if (input.Shifts[i].StartTime == input.Shifts[i].EndTime)
                modelState.AddModelError($"Shifts[{i}].EndTime", "The shift must end at a different time than it starts.");
        }
        foreach (var name in input.Shifts.GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key))
            modelState.AddModelError(string.Empty, $"Two shifts are named \"{name}\". Give each shift its own name.");
    }

    private async Task<IActionResult> EmployeesView(EmployeesInput input, CancellationToken ct)
    {
        var organization = (await context.GetAsync(ct))!;
        ViewData["ShiftOptions"] = await setup.ShiftsAsync(organization.UniqueId, ct);
        ViewData["NextCode"] = SetupService.NextCode(await setup.EmployeeCodesAsync(organization.UniqueId, ct));
        return View(nameof(Employees), input);
    }

    /// <summary>Redirects when the owner opens a step they cannot use yet (earlier steps unfinished) or no longer need.</summary>
    private async Task<IActionResult?> Guard(OnboardingStep step, CancellationToken ct)
    {
        if (await context.GetAsync(ct) is not { } organization) return NoOrganization();

        var next = OrganizationContext.NextStep(organization, User);
        if (next == OnboardingStep.Done) return ToStep(next); // later changes are made in the admin panel
        if (next == OnboardingStep.Password && step != OnboardingStep.Password) return ToStep(next);
        if (step == OnboardingStep.Password && next != OnboardingStep.Password) return ToStep(next);
        if (step > next) return ToStep(next);

        ViewData["Step"] = step;
        ViewData["NextStep"] = next;
        ViewData["Organization"] = organization.Name;
        return null;
    }

    private RedirectToActionResult ToStep(OnboardingStep step) => step == OnboardingStep.Done
        ? RedirectToAction("Index", "Dashboard", new { area = "Org" })
        : RedirectToAction(step.ToString());

    private IActionResult NoOrganization() => View("NoOrganization");
}
