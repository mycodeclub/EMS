using System.ComponentModel.DataAnnotations;
using EMS.Models.Common;

namespace EMS.Models.Onboarding;

public class ChangePasswordInput
{
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match."), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Organization profile, used by the onboarding wizard and the Profile page of the organization admin panel.</summary>
public class ProfileInput
{
    [Required, StringLength(200), Display(Name = "Organization name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(250), Display(Name = "Registered (legal) name")]
    public string? RegisteredName { get; set; }

    [Required]
    public Industry? Industry { get; set; }

    [StringLength(15), RegularExpression(Patterns.Gstin, ErrorMessage = "Enter a valid 15-character GSTIN, e.g. 27ABCDE1234F1Z5."), Display(Name = "GSTIN (optional)")]
    public string? GstNumber { get; set; }

    [Required, Phone, StringLength(20), Display(Name = "Office phone")]
    public string Phone { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150), Display(Name = "Office email")]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200), Display(Name = "Address line 1")]
    public string Line1 { get; set; } = string.Empty;

    [StringLength(200), Display(Name = "Address line 2")]
    public string? Line2 { get; set; }

    [Required, StringLength(100)] public string City { get; set; } = string.Empty;
    [Required, StringLength(100)] public string State { get; set; } = string.Empty;

    [Required, RegularExpression("^[1-9][0-9]{5}$", ErrorMessage = "Enter a 6-digit PIN code."), Display(Name = "PIN code")]
    public string PinCode { get; set; } = string.Empty;

    public static ProfileInput From(Organization o) => new()
    {
        Name = o.Name, RegisteredName = o.RegisteredName, Industry = o.Industry, GstNumber = o.GstNumber,
        Phone = o.Phone ?? string.Empty, Email = o.Email ?? string.Empty,
        Line1 = o.Address.Line1, Line2 = o.Address.Line2, City = o.Address.City, State = o.Address.State, PinCode = o.Address.PinCode,
    };

    /// <summary>Copies the profile onto the organization and its head office, which shares the address until branches are set up.</summary>
    public void ApplyTo(Organization o, Branch headOffice)
    {
        o.Name = Name.Trim();
        o.RegisteredName = RegisteredName?.Trim();
        o.Industry = Industry;
        o.GstNumber = string.IsNullOrWhiteSpace(GstNumber) ? null : GstNumber.Trim().ToUpperInvariant();
        o.Phone = Phone.Trim();
        o.Email = Email.Trim();
        o.Address.Line1 = Line1.Trim();
        o.Address.Line2 = Line2?.Trim();
        o.Address.City = City.Trim();
        o.Address.State = State.Trim();
        o.Address.PinCode = PinCode.Trim();

        headOffice.Address.Line1 = o.Address.Line1;
        headOffice.Address.Line2 = o.Address.Line2;
        headOffice.Address.City = o.Address.City;
        headOffice.Address.State = o.Address.State;
        headOffice.Address.PinCode = o.Address.PinCode;
        headOffice.Phone ??= o.Phone;
        headOffice.Email ??= o.Email;
    }
}

public class ShiftsInput
{
    /// <summary>"Do you run more than one shift, or operate 24x7?"</summary>
    [Required(ErrorMessage = "Please choose one.")]
    public bool? OperatesInShifts { get; set; }

    public List<ShiftRow> Shifts { get; set; } = [];

    public static List<ShiftRow> GeneralShift() => [new() { Name = "General", StartTime = new(9, 30), EndTime = new(18, 30), BreakMinutes = 60 }];

    /// <summary>Typical 8-hour rotation for hospitals, hotels, BPOs and call centres.</summary>
    public static List<ShiftRow> RoundTheClock() =>
    [
        new() { Name = "Morning", StartTime = new(6, 0), EndTime = new(14, 0), BreakMinutes = 30 },
        new() { Name = "Evening", StartTime = new(14, 0), EndTime = new(22, 0), BreakMinutes = 30 },
        new() { Name = "Night", StartTime = new(22, 0), EndTime = new(6, 0), BreakMinutes = 30 },
    ];
}

public class ShiftRow
{
    public int? Id { get; set; }

    [Required, StringLength(100), Display(Name = "Shift name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Starts")] public TimeOnly StartTime { get; set; }
    [Display(Name = "Ends")] public TimeOnly EndTime { get; set; }

    [Range(0, 240), Display(Name = "Break (min)")]
    public int BreakMinutes { get; set; }
}

public class EmployeesInput
{
    /// <summary>Rows without a first name are ignored, so the blank rows in the form can be left empty.</summary>
    public List<EmployeeRow> Rows { get; set; } = [];
}

public class EmployeeRow
{
    [StringLength(20)] public string? EmpCode { get; set; }
    [StringLength(100)] public string? FirstName { get; set; }
    [StringLength(100)] public string? LastName { get; set; }
    [Phone, StringLength(20)] public string? Mobile { get; set; }
    [StringLength(100)] public string? Designation { get; set; }
    public DateOnly? DateOfJoining { get; set; }
    public int? ShiftId { get; set; }
    [Range(0, 10_000_000)] public decimal? MonthlySalary { get; set; }

    public bool IsBlank => string.IsNullOrWhiteSpace(FirstName);
}
