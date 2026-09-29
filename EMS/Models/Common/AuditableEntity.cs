using System.ComponentModel.DataAnnotations;

namespace EMS.Models.Common;

/// <summary>
/// Base for master/transaction tables. Audit columns and soft delete are filled in ApplicationDbContext.SaveChanges:
/// times in UTC, *By = Identity user id of the logged-in user, or "system" for background jobs.
/// </summary>
public abstract class AuditableEntity : ISoftDelete
{
    [Key] public int UniqueId { get; set; }
    public DateTime CreatedAt { get; set; }
    [StringLength(450)] public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    [StringLength(450)] public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    [StringLength(450)] public string? DeletedBy { get; set; }
}
