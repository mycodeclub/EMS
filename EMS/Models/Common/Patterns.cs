namespace EMS.Models.Common;

/// <summary>Indian statutory identifier formats.</summary>
public static class Patterns
{
    public const string Gstin = "^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$";
    public const string Pan = "^[A-Z]{5}[0-9]{4}[A-Z]$";
    public const string Aadhaar = "^[2-9][0-9]{11}$";
    public const string Ifsc = "^[A-Z]{4}0[A-Z0-9]{6}$";
}
