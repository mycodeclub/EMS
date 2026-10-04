using EMS.Areas.Org.Models;
using EMS.Models.Common;
using EMS.Services.Leave;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.Areas.Org.Controllers;

/// <summary>HR's queue: approve or reject leave, accept resignations (fixing the last working day) or reject them.</summary>
[Authorize(Roles = AppRoles.PeopleManagers)]
public class RequestsController(OrganizationContext context, LeaveService leave, ResignationService resignations) : OrgController(context)
{
    private readonly OrganizationContext context = context;

    public async Task<IActionResult> Index(CancellationToken ct) => View(new RequestsPage(
        await leave.PendingAsync(Organization.UniqueId, ct),
        await resignations.OpenAsync(Organization.UniqueId, ct),
        await leave.RecentDecisionsAsync(Organization.UniqueId, 10, ct)));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(int id, bool approve, string? remarks, CancellationToken ct)
    {
        var approver = await context.EmployeeAsync(ct);
        var error = await leave.DecideAsync(Organization.UniqueId, id, approve, remarks, approver?.UniqueId, ct);
        if (error is null) TempData["Message"] = approve ? "Leave approved. The days are marked \"On leave\" in attendance." : "Leave rejected.";
        else TempData["Error"] = error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resignation(int id, bool accept, DateOnly? lastWorkingDay, string? remarks, CancellationToken ct)
    {
        var error = await resignations.DecideAsync(Organization.UniqueId, id, accept, lastWorkingDay, remarks, ct);
        if (error is null) TempData["Message"] = accept ? "Resignation accepted. The employee is now on notice until the last working day." : "Resignation rejected.";
        else TempData["Error"] = error;
        return RedirectToAction(nameof(Index));
    }
}
