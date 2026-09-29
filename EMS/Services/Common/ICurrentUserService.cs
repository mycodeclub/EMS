namespace EMS.Services.Common;

/// <summary>Who is making the change; recorded in CreatedBy / UpdatedBy / DeletedBy.</summary>
public interface ICurrentUserService
{
    /// <summary>Identity user id, or "system" outside a web request (background jobs, biometric sync).</summary>
    string UserId { get; }
}
