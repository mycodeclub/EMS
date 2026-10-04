using System.ComponentModel.DataAnnotations;
using EMS.Data;
using EMS.Models;
using Microsoft.EntityFrameworkCore;

namespace EMS.Services.Leave;

public class LeaveRequestInput
{
    [Required, Display(Name = "Leave type")] public int? LeaveTypeId { get; set; }
    [Required, Display(Name = "From")] public DateOnly? FromDate { get; set; }
    [Required, Display(Name = "To")] public DateOnly? ToDate { get; set; }
    [Display(Name = "Half day")] public bool IsHalfDay { get; set; }
    [Required, StringLength(1000), Display(Name = "Reason")] public string Reason { get; set; } = string.Empty;
}

/// <summary>An employee's balance for one leave type in a year, with what is waiting for approval.</summary>
public record LeaveBalanceRow(LeaveType Type, decimal Total, decimal Used, decimal Pending)
{
    public decimal Available => Total - Used - Pending;
}

/// <summary>
/// Leave types, yearly balances, applications and approvals. Organizations without leave types get the defaults
/// (casual, sick, earned) the first time leave is used. Chargeable days skip Sundays and the organization's holidays.
/// Approving a leave marks those days "On leave" in the attendance register and uses up the balance.
/// </summary>
public class LeaveService(ApplicationDbContext db)
{
    private static readonly (string Code, string Name, decimal Quota, bool CarryForward)[] Defaults =
    [
        ("CL", "Casual leave", 12, false),
        ("SL", "Sick leave", 8, false),
        ("EL", "Earned leave", 15, true),
    ];

    /// <summary>How far back an employee can apply (to regularise missed days).</summary>
    public const int BackdateDays = 30;

    public async Task<List<LeaveType>> TypesAsync(int organizationId, CancellationToken ct = default)
    {
        var types = await db.LeaveTypes.Where(t => t.OrganizationId == organizationId && t.IsActive).OrderBy(t => t.UniqueId).ToListAsync(ct);
        if (types.Count > 0) return types;

        types = Defaults.Select(d => new LeaveType
        {
            OrganizationId = organizationId, Code = d.Code, Name = d.Name, AnnualQuota = d.Quota,
            IsPaid = true, AllowCarryForward = d.CarryForward, MaxCarryForward = d.CarryForward ? 30 : null,
        }).ToList();
        db.LeaveTypes.AddRange(types);
        await db.SaveChangesAsync(ct);
        return types;
    }

    /// <summary>The employee's balances for <paramref name="year"/>, creating any missing rows (quota pro-rated for joiners that year).</summary>
    public async Task<List<LeaveBalanceRow>> BalancesAsync(Employee employee, int year, CancellationToken ct = default)
    {
        var types = await TypesAsync(employee.OrganizationId, ct);
        var balances = await db.EmployeeLeaveBalances.Where(b => b.EmployeeId == employee.UniqueId && b.Year == year).ToListAsync(ct);
        var added = false;
        foreach (var type in types.Where(t => balances.All(b => b.LeaveTypeId != t.UniqueId)))
        {
            var balance = new EmployeeLeaveBalance { EmployeeId = employee.UniqueId, LeaveTypeId = type.UniqueId, Year = year, Allocated = Allocation(type, employee, year) };
            db.EmployeeLeaveBalances.Add(balance);
            balances.Add(balance);
            added = true;
        }
        if (added) await db.SaveChangesAsync(ct);

        var pending = await db.LeaveApplications
            .Where(a => a.EmployeeId == employee.UniqueId && a.Status == LeaveStatus.Pending && a.FromDate.Year == year)
            .GroupBy(a => a.LeaveTypeId).Select(g => new { g.Key, Days = g.Sum(a => a.TotalDays) })
            .ToDictionaryAsync(x => x.Key, x => x.Days, ct);

        return types.Select(t =>
        {
            var b = balances.First(x => x.LeaveTypeId == t.UniqueId);
            return new LeaveBalanceRow(t, b.Total, b.Used, pending.GetValueOrDefault(t.UniqueId));
        }).ToList();
    }

    public Task<List<LeaveApplication>> ApplicationsAsync(int employeeId, CancellationToken ct = default) =>
        db.LeaveApplications.Include(a => a.LeaveType).Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.FromDate).ThenByDescending(a => a.UniqueId).ToListAsync(ct);

    /// <summary>Validates and saves a leave application. Returns an error message, or null when saved.</summary>
    public async Task<string?> ApplyAsync(Employee employee, LeaveRequestInput input, CancellationToken ct = default)
    {
        var from = input.FromDate!.Value;
        var to = input.ToDate!.Value;
        var today = DateOnly.FromDateTime(AppClock.Today);
        if (to < from) return "The leave cannot end before it starts.";
        if (from < today.AddDays(-BackdateDays)) return $"Leave can be applied at most {BackdateDays} days back.";
        if (from.Year != to.Year) return "Apply separately for the days in each year.";
        if (from < employee.DateOfJoining) return "The leave starts before your joining date.";
        if (employee.DateOfLeaving is { } left && to > left) return $"The leave runs past your last working day ({left:dd MMM yyyy}).";
        if (input.IsHalfDay && from != to) return "A half day can only be a single day.";

        var types = await TypesAsync(employee.OrganizationId, ct);
        if (types.FirstOrDefault(t => t.UniqueId == input.LeaveTypeId) is not { } type) return "Choose a leave type.";

        var overlapping = await db.LeaveApplications.AnyAsync(a => a.EmployeeId == employee.UniqueId
            && (a.Status == LeaveStatus.Pending || a.Status == LeaveStatus.Approved) && a.FromDate <= to && a.ToDate >= from, ct);
        if (overlapping) return "You already have leave applied for some of these days.";

        var days = (await ChargeableDaysAsync(employee.OrganizationId, from, to, ct)).Count;
        if (days == 0) return "These days are all Sundays or holidays; no leave is needed.";
        var total = input.IsHalfDay ? 0.5m : days;

        var balance = (await BalancesAsync(employee, from.Year, ct)).First(b => b.Type.UniqueId == type.UniqueId);
        if (total > balance.Available)
            return $"Not enough {type.Name.ToLowerInvariant()}: you asked for {total:0.#} day{(total == 1 ? "" : "s")} and have {balance.Available:0.#} available.";

        db.LeaveApplications.Add(new LeaveApplication
        {
            EmployeeId = employee.UniqueId, LeaveTypeId = type.UniqueId, FromDate = from, ToDate = to, IsHalfDay = input.IsHalfDay,
            TotalDays = total, Reason = input.Reason.Trim(), AppliedOn = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>The employee withdraws a pending application.</summary>
    public async Task<bool> CancelAsync(int employeeId, int applicationId, CancellationToken ct = default)
    {
        var application = await db.LeaveApplications.FirstOrDefaultAsync(a => a.UniqueId == applicationId && a.EmployeeId == employeeId, ct);
        if (application is not { Status: LeaveStatus.Pending }) return false;
        application.Status = LeaveStatus.Cancelled;
        application.ActionedOn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Pending applications in the organization, oldest first.</summary>
    public Task<List<LeaveApplication>> PendingAsync(int organizationId, CancellationToken ct = default) =>
        db.LeaveApplications.Include(a => a.LeaveType).Include(a => a.Employee)
            .Where(a => a.Employee.OrganizationId == organizationId && a.Status == LeaveStatus.Pending)
            .OrderBy(a => a.FromDate).ToListAsync(ct);

    public Task<List<LeaveApplication>> RecentDecisionsAsync(int organizationId, int count, CancellationToken ct = default) =>
        db.LeaveApplications.Include(a => a.LeaveType).Include(a => a.Employee)
            .Where(a => a.Employee.OrganizationId == organizationId && (a.Status == LeaveStatus.Approved || a.Status == LeaveStatus.Rejected) && a.ActionedOn != null)
            .OrderByDescending(a => a.ActionedOn).Take(count).ToListAsync(ct);

    /// <summary>
    /// Approves or rejects a pending application. Approval uses the balance and marks each chargeable day "On leave"
    /// in the attendance register (replacing what was marked). Returns an error message, or null when done.
    /// </summary>
    public async Task<string?> DecideAsync(int organizationId, int applicationId, bool approve, string? remarks, int? approverEmployeeId, CancellationToken ct = default)
    {
        var application = await db.LeaveApplications.Include(a => a.Employee).Include(a => a.LeaveType)
            .FirstOrDefaultAsync(a => a.UniqueId == applicationId && a.Employee.OrganizationId == organizationId, ct);
        if (application is null) return "That leave application was not found.";
        if (application.Status != LeaveStatus.Pending) return "That leave application has already been dealt with.";

        application.Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        application.ActionedOn = DateTime.UtcNow;
        application.ApprovedById = approverEmployeeId;
        application.ApproverRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        if (approve)
        {
            var balance = await db.EmployeeLeaveBalances.FirstAsync(b => b.EmployeeId == application.EmployeeId
                && b.LeaveTypeId == application.LeaveTypeId && b.Year == application.FromDate.Year, ct);
            balance.Used += application.TotalDays;

            var days = await ChargeableDaysAsync(organizationId, application.FromDate, application.ToDate, ct);
            var existing = (await db.Attendances
                    .Where(a => a.EmployeeId == application.EmployeeId && a.AttendanceDate >= application.FromDate && a.AttendanceDate <= application.ToDate)
                    .ToListAsync(ct))
                .GroupBy(a => a.AttendanceDate).ToDictionary(g => g.Key, g => g.OrderBy(a => a.UniqueId).First());
            var note = $"{(application.IsHalfDay ? "Half day " : "")}{application.LeaveType.Name.ToLowerInvariant()} (approved)";
            foreach (var day in days)
            {
                if (!existing.TryGetValue(day, out var record))
                {
                    record = new Attendance { EmployeeId = application.EmployeeId, ShiftId = application.Employee.ShiftId, AttendanceDate = day };
                    db.Attendances.Add(record);
                }
                record.Status = AttendanceStatus.OnLeave;
                record.Source = AttendanceSource.Manual;
                record.Remarks = note;
                if (!application.IsHalfDay)
                {
                    record.LoginAt = record.LogoffAt = null;
                    record.WorkedMinutes = null;
                    record.IsLate = false;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Days from <paramref name="from"/> to <paramref name="to"/> that are not Sundays or organization holidays.</summary>
    public async Task<List<DateOnly>> ChargeableDaysAsync(int organizationId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var holidays = (await db.Holidays.Where(h => h.OrganizationId == organizationId && h.Date >= from && h.Date <= to && !h.IsOptional)
            .Select(h => h.Date).ToListAsync(ct)).ToHashSet();
        var days = new List<DateOnly>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (day.DayOfWeek != DayOfWeek.Sunday && !holidays.Contains(day)) days.Add(day);
        }
        return days;
    }

    /// <summary>The yearly quota, pro-rated by whole months for someone who joined during the year, in half days.</summary>
    private static decimal Allocation(LeaveType type, Employee employee, int year)
    {
        if (employee.DateOfJoining.Year < year) return type.AnnualQuota;
        if (employee.DateOfJoining.Year > year) return 0;
        var months = 13 - employee.DateOfJoining.Month;
        return Math.Floor(type.AnnualQuota * months / 12 * 2) / 2;
    }
}
