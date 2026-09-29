namespace EMS.Models.Common;

/// <summary>Identity role names.</summary>
public static class AppRoles
{
    /// <summary>BitProSoftTech staff: sees enquiries, leads, customers and billing across all organizations.</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Customer's owner/admin: manages one organization's profile, shifts, staff, attendance and salary slips.</summary>
    public const string OrgAdmin = "OrgAdmin";
}
