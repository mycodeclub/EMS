using EMS.Services.Auth;
using Microsoft.AspNetCore.Mvc;

namespace EMS.Areas.Org.Controllers;

/// <summary>Ends a super admin's "sign in as customer" session. Lives under /Org so the browser sends the impersonation cookie.</summary>
[Area("Org")]
public class ImpersonationController(ILogger<ImpersonationController> logger) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> End()
    {
        if (User.IsImpersonated())
        {
            logger.LogInformation("{Admin} stopped viewing as {User}.", User.FindFirst(Impersonation.ImpersonatorNameClaim)?.Value, User.Identity?.Name);
            await Impersonation.SignOutAsync(HttpContext);
        }
        return RedirectToAction("Customers", "Admin", new { area = "" });
    }
}
