using EMS.Areas.Org.Models;
using EMS.Data;
using EMS.Models;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EMS.Areas.Org.Controllers;

public class EmployeesController(OrganizationContext context, ApplicationDbContext db, SetupService setup, ILogger<EmployeesController> logger)
    : OrgController(context)
{
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        var query = db.Employees.Include(e => e.Shift).Where(e => e.OrganizationId == Organization.UniqueId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.FirstName + " " + (e.LastName ?? ""), term)
                                     || EF.Functions.ILike(e.EmpCode, term)
                                     || (e.Designation != null && EF.Functions.ILike(e.Designation, term))
                                     || (e.Mobile != null && EF.Functions.ILike(e.Mobile, term)));
        }

        ViewData["Query"] = q;
        return View(await query.OrderBy(e => e.EmpCode).ToListAsync(ct));
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadOptionsAsync(ct);
        return View("Edit", new EmployeeInput { DateOfJoining = DateOnly.FromDateTime(DateTime.Today) });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { } employee) return NotFound();
        await LoadOptionsAsync(ct);
        return View(EmployeeInput.From(employee));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(EmployeeInput input, CancellationToken ct)
    {
        if (input.DateOfLeaving is { } left && input.DateOfJoining is { } joined && left < joined)
            ModelState.AddModelError(nameof(input.DateOfLeaving), "The leaving date cannot be before the joining date.");
        if (input.ShiftId is { } shiftId && !await db.Shifts.AnyAsync(s => s.UniqueId == shiftId && s.OrganizationId == Organization.UniqueId, ct))
            ModelState.AddModelError(nameof(input.ShiftId), "Choose one of your shifts.");

        Employee? employee = null;
        if (input.Id is { } id && (employee = await FindAsync(id, ct)) is null) return NotFound();

        if (employee is null)
        {
            var codes = await setup.EmployeeCodesAsync(Organization.UniqueId, ct);
            input.EmpCode = string.IsNullOrWhiteSpace(input.EmpCode) ? SetupService.NextCode(codes) : input.EmpCode.Trim().ToUpperInvariant();
            if (codes.Contains(input.EmpCode))
                ModelState.AddModelError(nameof(input.EmpCode), $"{input.EmpCode} is already used. Employee codes are never reused.");
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(ct);
            return View("Edit", input);
        }

        if (employee is null)
        {
            employee = new Employee
            {
                OrganizationId = Organization.UniqueId,
                BranchId = OrganizationContext.DefaultBranch(Organization).UniqueId,
                EmpCode = input.EmpCode!,
            };
            db.Employees.Add(employee);
        }
        input.ApplyTo(employee);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (BusinessRuleException ex)
        {
            logger.LogInformation("Employee save refused: {Reason}", ex.Message);
            db.ChangeTracker.Clear();
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadOptionsAsync(ct);
            return View("Edit", input);
        }

        TempData["Message"] = $"{employee.FullName} ({employee.EmpCode}) saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (await FindAsync(id, ct) is not { } employee) return NotFound();
        db.Employees.Remove(employee); // soft delete
        try
        {
            await db.SaveChangesAsync(ct);
            TempData["Message"] = $"{employee.FullName} removed.";
        }
        catch (BusinessRuleException)
        {
            db.ChangeTracker.Clear();
            TempData["Error"] = $"{employee.FullName} has attendance or leave records, so they cannot be removed. Set a leaving date instead.";
        }
        return RedirectToAction(nameof(Index));
    }

    private Task<Employee?> FindAsync(int id, CancellationToken ct) =>
        db.Employees.FirstOrDefaultAsync(e => e.UniqueId == id && e.OrganizationId == Organization.UniqueId, ct);

    private async Task LoadOptionsAsync(CancellationToken ct)
    {
        var shifts = await setup.ShiftsAsync(Organization.UniqueId, ct);
        ViewData["Shifts"] = shifts.Select(s => new SelectListItem($"{s.Name} ({s.StartTime:HH\\:mm}–{s.EndTime:HH\\:mm})", s.UniqueId.ToString())).ToList();
        ViewData["NextCode"] = SetupService.NextCode(await setup.EmployeeCodesAsync(Organization.UniqueId, ct));
    }
}
