using System.Globalization;

namespace EMS.Services.Import;

/// <summary>
/// Reads dates, times and numbers the way people type them in India, and the way Excel and CSV exports write them.
/// Day comes before month: 05/09/2026 is 5 September.
/// </summary>
public static class CellParser
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyy-M-d", "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "d.M.yyyy",
        "dd-MMM-yyyy", "d-MMM-yyyy", "dd MMM yyyy", "d MMM yyyy", "dd-MMM-yy", "d-MMM-yy", "dd/MM/yy", "d/M/yy", "dd-MM-yy", "d-M-yy",
        "yyyy/MM/dd", "yyyy/M/d",
    ];

    private static readonly string[] TimeFormats =
    [
        "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss", "HHmm", "hh:mm tt", "h:mm tt", "hh:mmtt", "h:mmtt", "hh:mm:ss tt", "h:mm:ss tt", "h tt", "htt",
    ];

    public static bool TryParseDate(string? text, out DateOnly date)
    {
        date = default;
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value)) return false;

        // A date and time together (an Excel date cell, or "2026-09-05 09:00"): keep the date.
        var space = value.IndexOf(' ');
        if (space > 0 && value.IndexOf(':') > space) value = value[..space];

        if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true;

        // Excel serial date (days since 1899-12-30), when the cell is formatted as a number.
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 20000 and < 80000)
        {
            date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
            return true;
        }
        return false;
    }

    public static bool TryParseTime(string? text, out TimeOnly time)
    {
        time = default;
        var value = text?.Trim().ToUpperInvariant().Replace(".", ":");
        if (string.IsNullOrEmpty(value)) return false;

        // A date and time together: keep the time.
        var space = value.IndexOf(' ');
        if (space > 0 && value.IndexOf(':') > space && !value.EndsWith("AM") && !value.EndsWith("PM")) value = value[(space + 1)..];

        if (TimeOnly.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out time)) return true;

        // Excel time as a fraction of a day (0.375 = 09:00), when the cell is formatted as a number.
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction) && fraction is >= 0 and < 1)
        {
            time = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(Math.Round(fraction * 1440)));
            return true;
        }
        return false;
    }

    /// <summary>Amounts with or without ₹, commas (1,25,000) or decimals.</summary>
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        var cleaned = new string((text ?? string.Empty).Where(c => char.IsDigit(c) || c is '.' or '-').ToArray());
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && cleaned.Length > 0;
    }
}
