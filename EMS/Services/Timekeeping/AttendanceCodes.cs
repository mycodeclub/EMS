using EMS.Models;

namespace EMS.Services.Timekeeping;

/// <summary>Short codes for each attendance status, used in the register, the day view and import files.</summary>
public static class AttendanceCodes
{
    public static readonly (AttendanceStatus Status, string Code, string Name)[] All =
    [
        (AttendanceStatus.Present, "P", "Present"),
        (AttendanceStatus.Absent, "A", "Absent"),
        (AttendanceStatus.HalfDay, "HD", "Half day"),
        (AttendanceStatus.OnLeave, "L", "On leave"),
        (AttendanceStatus.WeeklyOff, "WO", "Weekly off"),
        (AttendanceStatus.Holiday, "H", "Holiday"),
    ];

    public static string Code(AttendanceStatus status) => All.First(c => c.Status == status).Code;

    /// <summary>Accepts the code (P, HD…) or the name (Present, Half day, Leave…), in any case.</summary>
    public static bool TryParse(string? text, out AttendanceStatus status)
    {
        var key = Normalize(text);
        foreach (var (value, code, name) in All)
        {
            if (key == Normalize(code) || key == Normalize(name) || key == Normalize(value.ToString()))
            {
                status = value;
                return true;
            }
        }
        status = key == "leave" ? AttendanceStatus.OnLeave : default;
        return key == "leave";
    }

    private static string Normalize(string? text) =>
        new string((text ?? string.Empty).Where(char.IsLetter).ToArray()).ToLowerInvariant();
}
