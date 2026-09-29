namespace EMS.Data;

/// <summary>
/// Super admin account created at startup ("SuperAdmin" section of appsettings).
/// Keep the password out of source control outside development: set SuperAdmin__Password as an environment variable.
/// </summary>
public class SuperAdminOptions
{
    public const string Section = "SuperAdmin";

    public string? Email { get; set; }
    public string? Password { get; set; }
}
