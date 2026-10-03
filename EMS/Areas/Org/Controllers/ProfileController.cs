using Microsoft.AspNetCore.Authorization;
using EMS.Models.Common;
using EMS.Data;
using EMS.Models.Onboarding;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Mvc;

namespace EMS.Areas.Org.Controllers;

[Authorize(Roles = AppRoles.OrgAdmin)]
public class ProfileController(OrganizationContext context, ApplicationDbContext db) : OrgController(context)
{
    public IActionResult Index() => View(ProfileInput.From(Organization));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(input);
        input.ApplyTo(Organization, OrganizationContext.DefaultBranch(Organization));
        await db.SaveChangesAsync(ct);
        TempData["Message"] = "Organization profile saved.";
        return RedirectToAction(nameof(Index));
    }
}
