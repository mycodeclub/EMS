using EMS.Models;
using EMS.Models.Common;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EMS.Areas.Org.Controllers;

/// <summary>
/// Base for the organization panel: the signed-in user's organization, once onboarding is finished. Each controller
/// narrows the roles allowed (see AppRoles); the owner (OrgAdmin) can use every page.
/// </summary>
[Area("Org"), Authorize(Roles = AppRoles.OrgPanel)]
public abstract class OrgController(OrganizationContext context) : Controller
{
    protected Organization Organization { get; private set; } = null!;

    public override async Task OnActionExecutionAsync(ActionExecutingContext filterContext, ActionExecutionDelegate next)
    {
        var organization = await context.GetAsync(filterContext.HttpContext.RequestAborted);
        if (organization is null || OrganizationContext.NextStep(organization, User) != OnboardingStep.Done)
        {
            filterContext.Result = RedirectToAction("Index", "Onboarding", new { area = "" });
            return;
        }

        Organization = organization;
        ViewData["OrganizationName"] = organization.Name;
        ViewData["TrialEndsOn"] = organization.TrialEndsOn;
        ViewData["IsDemo"] = organization.IsDemo;
        await next();
    }

    /// <summary>Year and month from the query string, defaulting to the current month and never later than it.</summary>
    protected static (int Year, int Month) ResolveMonth(int? year, int? month)
    {
        var today = DateTime.Today;
        if (year is null || month is not (>= 1 and <= 12)) return (today.Year, today.Month);
        var requested = new DateOnly(year.Value, month.Value, 1);
        return requested > DateOnly.FromDateTime(today) ? (today.Year, today.Month) : (requested.Year, requested.Month);
    }
}
