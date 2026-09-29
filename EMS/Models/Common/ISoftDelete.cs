namespace EMS.Models.Common;

/// <summary>
/// Records are never physically deleted. ApplicationDbContext turns a Remove() into IsDeleted = true
/// and a global query filter hides deleted rows (use IgnoreQueryFilters() to see them).
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}
