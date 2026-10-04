using EMS.Data;
using EMS.Models;
using Microsoft.EntityFrameworkCore;

namespace EMS.Services.Leave;

/// <summary>
/// Resignations: the employee submits one (asking for a last day, by default the end of their notice period) and may
/// withdraw it while pending. HR accepts it with the final last working day, which puts the employee on notice and
/// sets their leaving date, or rejects it.
/// </summary>
public class ResignationService(ApplicationDbContext db)
{
    /// <summary>The employee's current resignation (pending or accepted), if any.</summary>
    public Task<Resignation?> CurrentAsync(int employeeId, CancellationToken ct = default) =>
        db.Resignations.Where(r => r.EmployeeId == employeeId && (r.Status == ResignationStatus.Pending || r.Status == ResignationStatus.Accepted))
            .OrderByDescending(r => r.SubmittedAt).FirstOrDefaultAsync(ct);

    public Task<List<Resignation>> HistoryAsync(int employeeId, CancellationToken ct = default) =>
        db.Resignations.Where(r => r.EmployeeId == employeeId).OrderByDescending(r => r.SubmittedAt).ToListAsync(ct);

    public async Task<string?> SubmitAsync(Employee employee, string reason, DateOnly requestedLastDay, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(AppClock.Today);
        if (await CurrentAsync(employee.UniqueId, ct) is not null) return "You already have a resignation in progress.";
        if (employee.DateOfLeaving is { } left && left < today) return "You have already left the organization.";
        if (requestedLastDay < today) return "The last working day cannot be in the past.";
        if (string.IsNullOrWhiteSpace(reason)) return "Give a reason for resigning.";

        db.Resignations.Add(new Resignation
        {
            EmployeeId = employee.UniqueId, SubmittedAt = DateTime.UtcNow, Reason = reason.Trim(), RequestedLastDay = requestedLastDay,
        });
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<bool> WithdrawAsync(int employeeId, int resignationId, CancellationToken ct = default)
    {
        var resignation = await db.Resignations.FirstOrDefaultAsync(r => r.UniqueId == resignationId && r.EmployeeId == employeeId, ct);
        if (resignation is not { Status: ResignationStatus.Pending }) return false;
        resignation.Status = ResignationStatus.Withdrawn;
        resignation.ActionedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<List<Resignation>> OpenAsync(int organizationId, CancellationToken ct = default) =>
        db.Resignations.Include(r => r.Employee)
            .Where(r => r.Employee.OrganizationId == organizationId && (r.Status == ResignationStatus.Pending || r.Status == ResignationStatus.Accepted))
            .OrderBy(r => r.Status).ThenBy(r => r.RequestedLastDay).ToListAsync(ct);

    /// <summary>Accepts (with <paramref name="lastWorkingDay"/>) or rejects a pending resignation. Returns an error, or null when done.</summary>
    public async Task<string?> DecideAsync(int organizationId, int resignationId, bool accept, DateOnly? lastWorkingDay, string? remarks, CancellationToken ct = default)
    {
        var resignation = await db.Resignations.Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.UniqueId == resignationId && r.Employee.OrganizationId == organizationId, ct);
        if (resignation is null) return "That resignation was not found.";
        if (resignation.Status != ResignationStatus.Pending) return "That resignation has already been dealt with.";

        var employee = resignation.Employee;
        if (accept)
        {
            var day = lastWorkingDay ?? resignation.RequestedLastDay;
            if (day < employee.DateOfJoining) return "The last working day cannot be before the joining date.";
            resignation.LastWorkingDay = day;
            employee.DateOfLeaving = day;
            employee.Status = EmployeeStatus.OnNotice;
        }

        resignation.Status = accept ? ResignationStatus.Accepted : ResignationStatus.Rejected;
        resignation.ActionedAt = DateTime.UtcNow;
        resignation.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (BusinessRuleException ex)
        {
            db.ChangeTracker.Clear();
            return ex.Message;
        }
        return null;
    }
}
