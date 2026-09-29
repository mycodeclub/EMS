using EMS.Models.Common;

namespace EMS.Services.Common;

/// <summary>
/// Common CRUD for every entity. Delete is always a soft delete; it is refused while active records
/// still reference the row, and restore is refused while a parent is deleted.
/// </summary>
public interface ICrudService<T> where T : AuditableEntity
{
    /// <summary>Composable query (add Where / Include / OrderBy, then ToListAsync or ToPagedResultAsync).</summary>
    IQueryable<T> Query(DeletedFilter deleted = DeletedFilter.Active);

    Task<T?> GetByIdAsync(int id, DeletedFilter deleted = DeletedFilter.Active, CancellationToken ct = default);

    Task<ServiceResult<T>> CreateAsync(T entity, CancellationToken ct = default);

    /// <summary>Copies the editable values of <paramref name="entity"/> onto the stored record with the same UniqueId.
    /// Audit and soft-delete columns cannot be changed this way.</summary>
    Task<ServiceResult<T>> UpdateAsync(T entity, CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);

    Task<ServiceResult> RestoreAsync(int id, CancellationToken ct = default);
}
