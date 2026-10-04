using EMS.Areas.Org.Models;
using EMS.Data;
using EMS.Models;
using EMS.Models.Common;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Areas.Org.Controllers;

/// <summary>
/// Printable letters (print / save as PDF): offer letter, an appraisal letter for each salary revision, and a relieving
/// letter once a resignation is accepted. Staff see their own; HR, accounts and the owner can open anyone's.
/// </summary>
public class LettersController(OrganizationContext context, ApplicationDbContext db) : OrgController(context)
{
    private readonly OrganizationContext context = context;

    public async Task<IActionResult> Index(int? employeeId, CancellationToken ct)
    {
        if (await ResolveAsync(employeeId, ct) is not { } employee) return Forbid();
        var revisions = await db.SalaryRevisions.Where(r => r.EmployeeId == employee.UniqueId).OrderByDescending(r => r.EffectiveFrom).ToListAsync(ct);
        return View(new LettersPage(employee, revisions, await AcceptedResignationAsync(employee.UniqueId, ct), employeeId is null));
    }

    public async Task<IActionResult> Offer(int? employeeId, CancellationToken ct)
    {
        if (await ResolveAsync(employeeId, ct) is not { } employee) return Forbid();
        // Salary at joining: before the first revision, if any.
        var first = await db.SalaryRevisions.Where(r => r.EmployeeId == employee.UniqueId).OrderBy(r => r.EffectiveFrom).FirstOrDefaultAsync(ct);
        ViewData["JoiningSalary"] = first?.PreviousSalary ?? employee.MonthlySalary ?? 0m;
        return Letter("Offer", employee, employeeId);
    }

    public async Task<IActionResult> Appraisal(int id, int? employeeId, CancellationToken ct)
    {
        if (await ResolveAsync(employeeId, ct) is not { } employee) return Forbid();
        if (await db.SalaryRevisions.FirstOrDefaultAsync(r => r.UniqueId == id && r.EmployeeId == employee.UniqueId, ct) is not { } revision) return NotFound();
        ViewData["Revision"] = revision;
        return Letter("Appraisal", employee, employeeId);
    }

    public async Task<IActionResult> Relieving(int? employeeId, CancellationToken ct)
    {
        if (await ResolveAsync(employeeId, ct) is not { } employee) return Forbid();
        if (await AcceptedResignationAsync(employee.UniqueId, ct) is not { } resignation) return NotFound();
        ViewData["Resignation"] = resignation;
        return Letter("Relieving", employee, employeeId);
    }

    private ViewResult Letter(string view, Employee employee, int? employeeId)
    {
        ViewData["Organization"] = Organization;
        ViewData["EmployeeId"] = employeeId;
        return View(view, employee);
    }

    private Task<Resignation?> AcceptedResignationAsync(int employeeId, CancellationToken ct) =>
        db.Resignations.Where(r => r.EmployeeId == employeeId && r.Status == ResignationStatus.Accepted)
            .OrderByDescending(r => r.ActionedAt).FirstOrDefaultAsync(ct);

    /// <summary>The signed-in employee (no id), or another employee of the organization for HR, accounts and the owner.</summary>
    private async Task<Employee?> ResolveAsync(int? employeeId, CancellationToken ct)
    {
        var self = await context.EmployeeAsync(ct);
        if (employeeId is null || employeeId == self?.UniqueId) return self;
        if (!AppRoles.OrgManagers.Split(',').Any(User.IsInRole)) return null;
        return await db.Employees.Include(e => e.Shift).FirstOrDefaultAsync(e => e.UniqueId == employeeId && e.OrganizationId == Organization.UniqueId, ct);
    }
}
