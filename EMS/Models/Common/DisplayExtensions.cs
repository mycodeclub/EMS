using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace EMS.Models.Common;

public static class DisplayExtensions
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>The enum member's [Display(Name)], or its name when it has none.</summary>
    public static string DisplayName(this Enum value) =>
        value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();

    /// <summary>Rupees with Indian digit grouping and no paise, e.g. ₹1,24,500.</summary>
    public static string ToRupees(this decimal amount) => amount.ToString("C0", India);

    public static string ToShortDate(this DateOnly date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public static string ToShortDate(this DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
}
