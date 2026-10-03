using Microsoft.AspNetCore.Authorization;
using EMS.Models.Common;
using EMS.Controllers;
using EMS.Models.Onboarding;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Mvc;

namespace EMS.Areas.Org.Controllers;

[Authorize(Roles = AppRoles.PeopleManagers)]
public class ShiftsController(OrganizationContext context, SetupService setup) : OrgController(context)
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var shifts = await setup.ShiftsAsync(Organization.UniqueId, ct);
        return View(new ShiftsInput
        {
            OperatesInShifts = Organization.OperatesInShifts,
            Shifts = shifts.Select(s => new ShiftRow { Id = s.UniqueId, Name = s.Name, StartTime = s.StartTime, EndTime = s.EndTime, BreakMinutes = s.BreakMinutes }).ToList(),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ShiftsInput input, CancellationToken ct)
    {
        if (input.OperatesInShifts == false) input.Shifts = input.Shifts.Take(1).ToList();
        OnboardingController.ValidateShifts(input, ModelState);
        if (ModelState.IsValid && await setup.SaveShiftsAsync(Organization, input.OperatesInShifts!.Value, input.Shifts, ct) is { } error)
            ModelState.AddModelError(string.Empty, error);

        if (!ModelState.IsValid) return View(input);
        TempData["Message"] = "Shifts saved.";
        return RedirectToAction(nameof(Index));
    }
}
