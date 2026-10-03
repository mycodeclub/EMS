namespace EMS.Models.Common;

/// <summary>Identity role names.</summary>
public static class AppRoles
{
    /// <summary>BitProSoftTech staff: sees enquiries, leads, customers and billing across all organizations.</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Customer's owner/admin: manages one organization's profile, shifts, staff, attendance and salary slips.</summary>
    public const string OrgAdmin = "OrgAdmin";

    /// <summary>Customer's HR staff: employees, attendance, shifts and imports, plus their own attendance and slips.</summary>
    public const string OrgHR = "OrgHR";

    /// <summary>Customer's accounts staff: salary slips for everyone, plus their own attendance and slips.</summary>
    public const string OrgAccounts = "OrgAccounts";

    /// <summary>Customer's employee: their own attendance and salary slips only.</summary>
    public const string Employee = "Employee";

    public static readonly string[] All = [SuperAdmin, OrgAdmin, OrgHR, OrgAccounts, Employee];

    // Comma-separated lists for [Authorize(Roles = ...)]: any one of the roles is enough.

    /// <summary>Everyone who signs in to an organization's panel.</summary>
    public const string OrgPanel = $"{OrgAdmin},{OrgHR},{OrgAccounts},{Employee}";

    /// <summary>The organization's dashboard.</summary>
    public const string OrgManagers = $"{OrgAdmin},{OrgHR},{OrgAccounts}";

    /// <summary>Employees, attendance, shifts and imports.</summary>
    public const string PeopleManagers = $"{OrgAdmin},{OrgHR}";

    /// <summary>Salary sheet and every employee's slip.</summary>
    public const string PayrollManagers = $"{OrgAdmin},{OrgAccounts}";

    /// <summary>Staff linked to an employee record, who see their own attendance and slips.</summary>
    public const string SelfService = $"{OrgHR},{OrgAccounts},{Employee}";
}
