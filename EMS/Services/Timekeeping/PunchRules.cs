using System.Globalization;
using EMS.Models;

namespace EMS.Services.Timekeeping;

/// <summary>What a day's punch in / out works out to under the employee's shift.</summary>
public record PunchResult(DateTime? LoginAt, DateTime? LogoffAt, int? WorkedMinutes, int LateMinutes, AttendanceStatus? Status)
{
    public bool IsLate => LateMinutes > 0;
}

/// <summary>
/// Turns punch in / out times into worked time, late arrival and a suggested status, using the shift timings.
/// The same rules run in the browser (console.js) so the day view shows the result while typing.
/// </summary>
public static class PunchRules
{
    /// <summary>
    /// <paramref name="date"/> is the day the shift starts. The punch in is on that day, except on a night shift, where it is
    /// placed nearest the shift start: 00:10 on a 22:00–06:00 shift is just after midnight. The punch out is the first time
    /// after the punch in, so 06:00 is the next morning.
    /// Status: present from the shift's full-day minutes, half day from its half-day minutes, otherwise absent.
    /// With a punch in but no punch out yet, the employee counts as present.
    /// </summary>
    public static PunchResult Evaluate(Shift? shift, DateOnly date, TimeOnly? punchIn, TimeOnly? punchOut)
    {
        if (punchIn is not { } timeIn) return new(null, null, null, 0, null);
        timeIn = Truncate(timeIn);

        var loginAt = date.ToDateTime(timeIn);
        var late = 0;
        if (shift is not null)
        {
            var offset = shift.CrossesMidnight ? MinutesAfter(shift.StartTime, timeIn) : (int)(timeIn.ToTimeSpan() - shift.StartTime.ToTimeSpan()).TotalMinutes;
            if (shift.CrossesMidnight) loginAt = date.ToDateTime(shift.StartTime).AddMinutes(offset);
            if (offset > shift.GraceMinutes) late = offset;
        }

        if (punchOut is not { } timeOut) return new(loginAt, null, null, late, AttendanceStatus.Present);

        var worked = (int)(Truncate(timeOut) - timeIn).TotalMinutes; // TimeOnly subtraction wraps past midnight
        var status = shift is null || shift.FullDayMinutes <= 0 || worked >= shift.FullDayMinutes ? AttendanceStatus.Present
            : worked >= shift.HalfDayMinutes && shift.HalfDayMinutes > 0 ? AttendanceStatus.HalfDay
            : AttendanceStatus.Absent;
        return new(loginAt, loginAt.AddMinutes(worked), worked, late, status);
    }

    /// <summary>Minutes from <paramref name="start"/> to <paramref name="time"/>, between -12 and +12 hours.</summary>
    private static int MinutesAfter(TimeOnly start, TimeOnly time)
    {
        var minutes = (int)(Truncate(time) - start).TotalMinutes;
        return minutes > 720 ? minutes - 1440 : minutes;
    }

    /// <summary>9h 05m</summary>
    public static string Duration(int minutes) => minutes < 60 ? $"{minutes}m" : $"{minutes / 60}h {minutes % 60:00}m";

    /// <summary>24-hour clock, as typed into the time inputs and templates.</summary>
    public static string Clock(DateTime time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Clock(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static TimeOnly Truncate(TimeOnly time) => new(time.Hour, time.Minute);
}
