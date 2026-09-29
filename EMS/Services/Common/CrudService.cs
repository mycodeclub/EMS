using EMS.Data;
using EMS.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace EMS.Services.Common;

public class CrudService<T>(ApplicationDbContext db, ILogger<CrudService<T>> logger) : ICrudService<T> where T : AuditableEntity
{
    /// <summary>Maintained by ApplicationDbContext only; never copied from user input.</summary>
    private static readonly string[] ProtectedProperties =
    [
        nameof(AuditableEntity.UniqueId),
        nameof(AuditableEntity.CreatedAt), nameof(AuditableEntity.CreatedBy),
        nameof(AuditableEntity.UpdatedAt), nameof(AuditableEntity.UpdatedBy),
        nameof(AuditableEntity.IsDeleted), nameof(AuditableEntity.DeletedAt), nameof(AuditableEntity.DeletedBy),
    ];

    protected ApplicationDbContext Db => db;
    protected DbSet<T> Set => db.Set<T>();

    public virtual IQueryable<T> Query(DeletedFilter deleted = DeletedFilter.Active) => Set.WithDeleted(deleted);

    public virtual Task<T?> GetByIdAsync(int id, DeletedFilter deleted = DeletedFilter.Active, CancellationToken ct = default) =>
        Query(deleted).FirstOrDefaultAsync(e => e.UniqueId == id, ct);

    public virtual async Task<ServiceResult<T>> CreateAsync(T entity, CancellationToken ct = default)
    {
        entity.UniqueId = 0;
        entity.IsDeleted = false;
        Set.Add(entity);

        var error = await TrySaveAsync(ct);
        return error is null ? ServiceResult<T>.Success(entity) : ServiceResult<T>.Failure(error);
    }

    public virtual async Task<ServiceResult<T>> UpdateAsync(T entity, CancellationToken ct = default)
    {
        var existing = await Set.FirstOrDefaultAsync(e => e.UniqueId == entity.UniqueId, ct);
        if (existing is null) return ServiceResult<T>.Failure(NotFound(entity.UniqueId));

        var entry = db.Entry(existing);
        entry.CurrentValues.SetValues(entity);
        foreach (var name in ProtectedProperties)
        {
            var property = entry.Property(name);
            property.CurrentValue = property.OriginalValue;
        }

        // Owned values (e.g. Address) are separate entries and are not covered by SetValues above.
        foreach (var owned in entry.References.Where(r => r.Metadata.TargetEntityType.IsOwned()))
        {
            var incoming = owned.Metadata.PropertyInfo?.GetValue(entity);
            if (incoming is null) continue;
            if (owned.TargetEntry is null) owned.CurrentValue = incoming;
            else owned.TargetEntry.CurrentValues.SetValues(incoming);
        }

        var error = await TrySaveAsync(ct);
        return error is null ? ServiceResult<T>.Success(existing) : ServiceResult<T>.Failure(error);
    }

    public virtual async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await Set.FirstOrDefaultAsync(e => e.UniqueId == id, ct);
        if (existing is null) return ServiceResult.Failure(NotFound(id));

        Set.Remove(existing); // turned into a soft delete by ApplicationDbContext
        var error = await TrySaveAsync(ct);
        return error is null ? ServiceResult.Success() : ServiceResult.Failure(error);
    }

    public virtual async Task<ServiceResult> RestoreAsync(int id, CancellationToken ct = default)
    {
        var existing = await Query(DeletedFilter.Deleted).FirstOrDefaultAsync(e => e.UniqueId == id, ct);
        if (existing is null) return ServiceResult.Failure($"No deleted {typeof(T).Name} #{id} found.");

        existing.IsDeleted = false;
        existing.DeletedAt = null;
        existing.DeletedBy = null;

        var error = await TrySaveAsync(ct);
        return error is null ? ServiceResult.Success() : ServiceResult.Failure(error);
    }

    /// <summary>Saves; on a rule or uniqueness violation returns a user-facing message and discards the pending changes.</summary>
    protected async Task<string?> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (BusinessRuleException ex)
        {
            db.ChangeTracker.Clear();
            return ex.Message;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            logger.LogInformation(ex, "Unique constraint violated saving {Entity}", typeof(T).Name);
            return $"A {typeof(T).Name} with the same code or key already exists.";
        }
    }

    private static string NotFound(int id) => $"{typeof(T).Name} #{id} was not found.";

    // PostgreSQL: 23505 "duplicate key value violates unique constraint"; SQLite: "UNIQUE constraint failed".
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message is { } message
        && (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase));
}
