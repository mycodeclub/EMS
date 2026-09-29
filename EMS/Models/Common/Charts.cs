namespace EMS.Models.Common;

/// <summary>One bar or column: axis label, value, and the formatted value shown in labels and tooltips.</summary>
public record ChartPoint(string Label, decimal Value, string Display);

/// <summary>Vertical columns for a value over time (single series).</summary>
public record ColumnChart(string Title, string? Subtitle, IReadOnlyList<ChartPoint> Points, Func<decimal, string> FormatAxis)
{
    public decimal Max => Charts.NiceCeiling(Points.Count == 0 ? 0 : Points.Max(p => p.Value));
}

/// <summary>Horizontal bars comparing categories (single series).</summary>
public record BarChart(string Title, string? Subtitle, IReadOnlyList<ChartPoint> Points)
{
    public decimal Max => Points.Count == 0 ? 0 : Points.Max(p => p.Value);
}

/// <summary>Stacked columns; each column has one value per series, in <see cref="Series"/> order (at most three series).</summary>
public record StackedChart(string Title, string? Subtitle, IReadOnlyList<string> Series, IReadOnlyList<StackedColumn> Columns)
{
    /// <summary>Counts of people: an even ceiling of at least 2, so the midline label is a whole number.</summary>
    public decimal Max
    {
        get
        {
            var ceiling = Charts.NiceCeiling(Columns.Count == 0 ? 0 : Columns.Max(c => c.Values.Sum()));
            return Math.Max(2, Math.Ceiling(ceiling / 2) * 2);
        }
    }

    public bool IsEmpty => Columns.All(c => c.Values.Sum() == 0);
}

public record StackedColumn(string Label, IReadOnlyList<decimal> Values);

public static class Charts
{
    /// <summary>Rounds up to 1, 2, 2.5 or 5 × a power of ten, so gridlines fall on round numbers.</summary>
    public static decimal NiceCeiling(decimal value)
    {
        if (value <= 0) return 1;
        var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)value)));
        foreach (var step in new[] { 1m, 2m, 2.5m, 5m, 10m })
        {
            if (value <= step * magnitude) return step * magnitude;
        }
        return 10 * magnitude;
    }

    /// <summary>Percentage of <paramref name="max"/>, for CSS heights and widths.</summary>
    public static string Percent(decimal value, decimal max) =>
        (max <= 0 ? 0 : Math.Round(value / max * 100, 2)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

    /// <summary>Compact rupees for axes: ₹2.5L, ₹1.2Cr, ₹40K.</summary>
    public static string CompactRupees(decimal amount) => amount switch
    {
        >= 1_00_00_000 => $"₹{amount / 1_00_00_000:0.##}Cr",
        >= 1_00_000 => $"₹{amount / 1_00_000:0.##}L",
        >= 1_000 => $"₹{amount / 1_000:0.##}K",
        _ => $"₹{amount:0}",
    };
}
