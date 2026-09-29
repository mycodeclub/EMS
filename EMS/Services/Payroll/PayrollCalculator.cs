using EMS.Models;

namespace EMS.Services.Payroll;

/// <summary>One employee's pay for a month.</summary>
public record PayLine(
    Employee Employee, int Year, int Month, int DaysInMonth, int EmployedDays,
    decimal PaidDays, decimal LossOfPayDays, int UnmarkedDays,
    IReadOnlyDictionary<AttendanceStatus, int> Counts,
    decimal Gross, decimal Earned)
{
    public decimal Deduction => Gross - Earned;
}

/// <summary>
/// Monthly salary is pro-rated by paid days over calendar days. Present, leave, holiday and weekly off are paid,
/// a half day is paid half, absent and unmarked days are not. Statutory deductions (PF, ESI, PT, TDS) are not applied yet.
/// </summary>
public static class PayrollCalculator
{
    public static decimal PaidWeight(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Absent => 0m,
        AttendanceStatus.HalfDay => 0.5m,
        _ => 1m,
    };

    /// <summary>Days of the month on which the employee was on the rolls (joined, not yet left).</summary>
    public static (DateOnly From, DateOnly To)? EmployedRange(Employee employee, int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var from = employee.DateOfJoining > first ? employee.DateOfJoining : first;
        var to = employee.DateOfLeaving is { } left && left < last ? left : last;
        return from <= to ? (from, to) : null;
    }

    public static PayLine Calculate(Employee employee, int year, int month, IEnumerable<Attendance> records)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var range = EmployedRange(employee, year, month);
        var employedDays = range is { } r ? r.To.DayNumber - r.From.DayNumber + 1 : 0;

        // One status per day (the first record, if a day has several shifts).
        var byDay = records
            .Where(a => range is { } rr && a.AttendanceDate >= rr.From && a.AttendanceDate <= rr.To)
            .GroupBy(a => a.AttendanceDate)
            .Select(g => g.OrderBy(a => a.UniqueId).First().Status)
            .ToList();

        var counts = byDay.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
        var paid = byDay.Sum(PaidWeight);
        var gross = employee.MonthlySalary ?? 0m;
        var earned = Math.Round(gross * paid / daysInMonth, 0, MidpointRounding.AwayFromZero);

        return new PayLine(employee, year, month, daysInMonth, employedDays, paid, employedDays - paid,
            employedDays - byDay.Count, counts, gross, earned);
    }
}
