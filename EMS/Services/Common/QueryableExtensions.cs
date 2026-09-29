using EMS.Data;
using EMS.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace EMS.Services.Common;

public static class QueryableExtensions
{
    /// <summary>Applies a <see cref="DeletedFilter"/>. Only the soft-delete filter is lifted; other query filters stay.</summary>
    public static IQueryable<T> WithDeleted<T>(this IQueryable<T> query, DeletedFilter filter) where T : class, ISoftDelete =>
        filter switch
        {
            DeletedFilter.Active => query,
            DeletedFilter.Deleted => query.IgnoreQueryFilters([ApplicationDbContext.SoftDeleteFilter]).Where(e => e.IsDeleted),
            DeletedFilter.All => query.IgnoreQueryFilters([ApplicationDbContext.SoftDeleteFilter]),
            _ => throw new ArgumentOutOfRangeException(nameof(filter)),
        };

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, PagedResult<T>.MaxPageSize);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page, pageSize, total);
    }
}
