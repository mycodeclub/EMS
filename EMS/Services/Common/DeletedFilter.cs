namespace EMS.Services.Common;

/// <summary>Which rows to read with respect to soft delete.</summary>
public enum DeletedFilter
{
    /// <summary>Default everywhere: only records that are not deleted.</summary>
    Active,
    /// <summary>Only deleted records (e.g. a recycle bin / restore screen).</summary>
    Deleted,
    /// <summary>Both, for audit and history reports.</summary>
    All,
}
