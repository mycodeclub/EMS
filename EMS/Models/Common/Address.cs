using System.ComponentModel.DataAnnotations;

namespace EMS.Models.Common;

/// <summary>Owned value type; stored as columns on the owning table (Organization, Branch).</summary>
public class Address
{
    [Required, StringLength(200)] public string Line1 { get; set; } = string.Empty;
    [StringLength(200)] public string? Line2 { get; set; }
    [Required, StringLength(100)] public string City { get; set; } = string.Empty;
    [Required, StringLength(100)] public string State { get; set; } = string.Empty;
    [Required, StringLength(10)] public string PinCode { get; set; } = string.Empty;
    [StringLength(100)] public string Country { get; set; } = "India";
}
