using System.Linq.Expressions;
using EMS.Models;
using EMS.Models.Common;
using EMS.Services.Common;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace EMS.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUser)
    : IdentityDbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<ContactPerson> ContactPersons => Set<ContactPerson>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<EmployeeLeaveBalance> EmployeeLeaveBalances => Set<EmployeeLeaveBalance>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<BiometricPunch> BiometricPunches => Set<BiometricPunch>();
    public DbSet<BiometricIdAssignment> BiometricIdAssignments => Set<BiometricIdAssignment>();

    /// <summary>Name of the global soft-delete query filter (lift it with IgnoreQueryFilters([SoftDeleteFilter])).</summary>
    public const string SoftDeleteFilter = "SoftDelete";

    // Unique indexes ignore soft-deleted rows, so e.g. a shift code can be reused after deletion.
    private const string NotDeleted = "\"IsDeleted\" = 0";

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Leave days: supports half days, e.g. 12.5
        configurationBuilder.Properties<decimal>().HavePrecision(6, 2);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>().OwnsOne(o => o.Address);
        builder.Entity<Branch>(b =>
        {
            b.OwnsOne(x => x.Address);
            b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique().HasFilter(NotDeleted);
        });

        builder.Entity<Shift>().HasIndex(s => new { s.OrganizationId, s.Code }).IsUnique().HasFilter(NotDeleted);

        builder.Entity<Employee>(e =>
        {
            // Employee codes are never reused, not even after the employee is deleted.
            e.HasIndex(x => new { x.OrganizationId, x.EmpCode }).IsUnique();
            // Not unique: a leaver keeps their last AttendanceId while a new joiner reuses it. Uniqueness is on BiometricIdAssignment.
            e.HasIndex(x => new { x.OrganizationId, x.AttendanceId });
            e.HasIndex(x => x.UserId).IsUnique().HasFilter($"\"UserId\" IS NOT NULL AND {NotDeleted}");
            e.HasOne(x => x.ReportingManager).WithMany().HasForeignKey(x => x.ReportingManagerId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        builder.Entity<LeaveType>().HasIndex(l => new { l.OrganizationId, l.Code }).IsUnique().HasFilter(NotDeleted);

        builder.Entity<EmployeeLeaveBalance>()
            .HasIndex(b => new { b.EmployeeId, b.LeaveTypeId, b.Year }).IsUnique().HasFilter(NotDeleted);

        builder.Entity<Holiday>().HasIndex(h => new { h.OrganizationId, h.BranchId, h.Date }).IsUnique().HasFilter(NotDeleted);

        builder.Entity<LeaveApplication>(l =>
        {
            l.HasOne(x => x.Employee).WithMany(e => e.LeaveApplications).HasForeignKey(x => x.EmployeeId);
            l.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById);
            l.HasIndex(x => new { x.EmployeeId, x.FromDate });
        });

        // ShiftId is part of the key so double shifts (common in hospitals) are allowed on the same day.
        builder.Entity<Attendance>().HasIndex(a => new { a.EmployeeId, a.AttendanceDate, a.ShiftId }).IsUnique().HasFilter(NotDeleted);

        builder.Entity<BiometricPunch>(p =>
        {
            // Re-syncing the same device log must not create duplicates (nor revive soft-deleted punches).
            p.HasIndex(x => new { x.OrganizationId, x.AttendanceId, x.PunchTime }).IsUnique();
            p.HasIndex(x => x.IsProcessed);
        });

        builder.Entity<BiometricIdAssignment>(a =>
        {
            a.HasOne(x => x.Employee).WithMany(e => e.BiometricIdHistory).HasForeignKey(x => x.EmployeeId);
            // One current holder per ID; overlapping past tenures are prevented in TrackBiometricIdsAsync.
            a.HasIndex(x => new { x.OrganizationId, x.AttendanceId }).IsUnique().HasFilter($"\"ValidTo\" IS NULL AND {NotDeleted}");
            a.HasIndex(x => new { x.OrganizationId, x.AttendanceId, x.ValidFrom });
        });

        // Hide soft-deleted rows from every query.
        foreach (var type in builder.Model.GetEntityTypes()
                     .Where(t => typeof(ISoftDelete).IsAssignableFrom(t.ClrType) && !t.IsOwned()))
        {
            var p = Expression.Parameter(type.ClrType, "e");
            var body = Expression.Not(Expression.Property(p, nameof(ISoftDelete.IsDeleted)));
            builder.Entity(type.ClrType).HasQueryFilter(SoftDeleteFilter, Expression.Lambda(body, p));
        }

        // HR records must never disappear through cascade deletes; deactivate instead.
        foreach (var fk in builder.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace == typeof(Employee).Namespace)
                     .SelectMany(t => t.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        // async: false completes synchronously, so blocking here cannot deadlock.
        PrepareSaveAsync(async: false, CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await PrepareSaveAsync(async: true, cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task PrepareSaveAsync(bool async, CancellationToken ct)
    {
        foreach (var check in GetDeleteChecks().Concat(GetRestoreChecks()))
        {
            var count = async
                ? await ((IAsyncQueryProvider)check.Provider).ExecuteAsync<Task<int>>(check.CountQuery, ct)
                : check.Provider.Execute<int>(check.CountQuery);
            if (check.Evaluate(count) is { } error) throw error;
        }

        await TrackBiometricIdsAsync(async, ct);
        ApplyRules();
    }

    /// <summary>Keeps BiometricIdAssignment (ID -> employee tenure history) in step with employee changes.</summary>
    private async Task TrackBiometricIdsAsync(bool async, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        foreach (var entry in ChangeTracker.Entries<Employee>().ToList())
        {
            var emp = entry.Entity;
            if (entry.State is EntityState.Added or EntityState.Modified && string.IsNullOrWhiteSpace(emp.AttendanceId))
                emp.AttendanceId = emp.EmpCode; // biometric ID equals employee code unless the device uses a different one

            switch (entry.State)
            {
                case EntityState.Added:
                    await OpenAssignmentAsync(emp, emp.DateOfJoining, async, ct);
                    break;

                case EntityState.Modified:
                    var idProperty = entry.Property(e => e.AttendanceId);
                    if (idProperty.OriginalValue != idProperty.CurrentValue)
                    {
                        if (emp.DateOfLeaving is not null)
                            throw new BusinessRuleException($"Cannot change the biometric ID of employee {emp.EmpCode}: the employee has left.");

                        var current = await CurrentAssignmentAsync(emp.UniqueId, async, ct);
                        var from = today;
                        if (current is not null)
                        {
                            if (current.ValidFrom >= today)
                            {
                                // Corrected before the tenure started: the old entry was a mistake.
                                from = current.ValidFrom;
                                Remove(current);
                            }
                            else
                            {
                                current.ValidTo = today.AddDays(-1);
                            }
                        }
                        await OpenAssignmentAsync(emp, from, async, ct);
                    }

                    // Restored employee who has not left: they hold their biometric ID again from today.
                    if (entry.Property(e => e.IsDeleted).OriginalValue && !emp.IsDeleted && emp.DateOfLeaving is null
                        && await CurrentAssignmentAsync(emp.UniqueId, async, ct) is null)
                    {
                        await OpenAssignmentAsync(emp, today, async, ct);
                    }

                    if (entry.Property(e => e.DateOfLeaving).IsModified && emp.DateOfLeaving is { } left
                        && await CurrentAssignmentAsync(emp.UniqueId, async, ct) is { } open)
                    {
                        open.ValidTo = left;
                    }
                    break;

                case EntityState.Deleted:
                    if (await CurrentAssignmentAsync(emp.UniqueId, async, ct) is { } held)
                        held.ValidTo = emp.DateOfLeaving ?? today;
                    break;
            }
        }
    }

    private async Task<BiometricIdAssignment?> CurrentAssignmentAsync(int employeeId, bool async, CancellationToken ct)
    {
        var query = BiometricIdAssignments.Where(a => a.EmployeeId == employeeId && a.ValidTo == null);
        return async ? await query.FirstOrDefaultAsync(ct) : query.FirstOrDefault();
    }

    private async Task OpenAssignmentAsync(Employee emp, DateOnly from, bool async, CancellationToken ct)
    {
        var organizationId = emp.Organization?.UniqueId ?? emp.OrganizationId;

        // Lift soft delete so tenures of deleted employees still count; deleted assignments (corrections) do not.
        var clashQuery = BiometricIdAssignments.IgnoreQueryFilters([SoftDeleteFilter])
            .Where(a => !a.IsDeleted && a.OrganizationId == organizationId && a.AttendanceId == emp.AttendanceId
                        && a.EmployeeId != emp.UniqueId && (a.ValidTo == null || a.ValidTo >= from))
            .Select(a => new { a.Employee.EmpCode, a.ValidFrom, a.ValidTo });
        var clash = async ? await clashQuery.FirstOrDefaultAsync(ct) : clashQuery.FirstOrDefault();
        if (clash is not null)
        {
            throw new BusinessRuleException(
                $"Biometric ID {emp.AttendanceId} is assigned to employee {clash.EmpCode} from {clash.ValidFrom:dd-MMM-yyyy} to "
                + $"{(clash.ValidTo is { } to ? to.ToString("dd-MMM-yyyy") : "present")}. It can be reused only after that tenure ends.");
        }

        BiometricIdAssignments.Add(new BiometricIdAssignment
        {
            OrganizationId = organizationId,
            Organization = emp.Organization!, // null when only OrganizationId is set; EF then uses the id
            Employee = emp,
            AttendanceId = emp.AttendanceId,
            ValidFrom = from,
        });
    }

    /// <summary>
    /// For every record being deleted, one COUNT query per table that references it. The global query filter
    /// makes the count skip soft-deleted children; children deleted in this same SaveChanges are subtracted.
    /// </summary>
    private List<PendingCheck> GetDeleteChecks()
    {
        var checks = new List<PendingCheck>();
        var deleting = ChangeTracker.Entries()
            .Where(e => e is { State: EntityState.Deleted, Entity: ISoftDelete })
            .ToList();

        foreach (var parent in deleting)
        {
            // History rows (biometric ID tenures) are kept and never block a delete.
            foreach (var fk in parent.Metadata.GetReferencingForeignKeys()
                         .Where(f => !f.IsOwnership && f.DeclaringEntityType.ClrType != typeof(BiometricIdAssignment)))
            {
                var key = parent.Property(fk.PrincipalKey.Properties[0].Name).CurrentValue!;
                var fkName = fk.Properties[0].Name;
                var childType = fk.DeclaringEntityType;
                var deletedTogether = deleting.Count(e => e.Metadata == childType && Equals(e.Property(fkName).OriginalValue, key));

                checks.Add(CountWhere(childType.ClrType, fkName, fk.Properties[0].ClrType, key, count =>
                    count - deletedTogether > 0
                        ? new DeleteBlockedException(parent.Metadata.ClrType.Name, key, childType.ClrType.Name, count - deletedTogether)
                        : null));
            }
        }

        return checks;
    }

    /// <summary>A record being restored (IsDeleted true -> false) needs every parent it points to be active.</summary>
    private List<PendingCheck> GetRestoreChecks()
    {
        var checks = new List<PendingCheck>();
        var restoring = ChangeTracker.Entries()
            .Where(e => e is { State: EntityState.Modified, Entity: ISoftDelete { IsDeleted: false } }
                        && (bool)e.Property(nameof(ISoftDelete.IsDeleted)).OriginalValue!);

        foreach (var child in restoring)
        {
            foreach (var fk in child.Metadata.GetForeignKeys()
                         .Where(f => !f.IsOwnership && typeof(ISoftDelete).IsAssignableFrom(f.PrincipalEntityType.ClrType)))
            {
                if (child.Property(fk.Properties[0].Name).CurrentValue is not { } parentKey) continue;

                var key = fk.PrincipalKey.Properties[0];
                var parentType = fk.PrincipalEntityType.ClrType;
                checks.Add(CountWhere(parentType, key.Name, key.ClrType, parentKey, count =>
                    count == 0
                        ? new RestoreBlockedException(child.Metadata.ClrType.Name, child.Property(nameof(AuditableEntity.UniqueId)).CurrentValue!, parentType.Name, parentKey)
                        : null));
            }
        }

        return checks;
    }

    /// <summary>Builds <c>Set&lt;T&gt;().Count(e => e.Property == value)</c> for a type known only at runtime (query filters apply).</summary>
    private PendingCheck CountWhere(Type entityType, string propertyName, Type propertyType, object value, Func<int, Exception?> evaluate)
    {
        var p = Expression.Parameter(entityType, "e");
        var predicate = Expression.Lambda(
            Expression.Equal(Expression.Property(p, propertyName), Expression.Constant(value, propertyType)), p);
        var set = (IQueryable)GetType().GetMethod(nameof(Set), Type.EmptyTypes)!
            .MakeGenericMethod(entityType).Invoke(this, null)!;
        var count = Expression.Call(typeof(Queryable), nameof(Queryable.Count), [entityType],
            Expression.Call(typeof(Queryable), nameof(Queryable.Where), [entityType], set.Expression, Expression.Quote(predicate)));

        return new PendingCheck(set.Provider, count, evaluate);
    }

    private sealed record PendingCheck(IQueryProvider Provider, Expression CountQuery, Func<int, Exception?> Evaluate);

    private void ApplyRules()
    {
        var now = DateTime.UtcNow;
        var user = currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = user;
            }
            else if (entry.State == EntityState.Modified
                     || entry.References.Any(r => r.TargetEntry is { State: EntityState.Modified } t && t.Metadata.IsOwned()))
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = user;
            }
        }

        // Never physically delete: convert Remove() into a soft delete.
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted).ToList())
        {
            if (entry.Entity is ISoftDelete softDelete)
            {
                entry.State = EntityState.Modified;
                softDelete.IsDeleted = true;
                softDelete.DeletedAt = now;
                softDelete.DeletedBy = user;
            }
            else if (entry.Metadata.IsOwned())
            {
                // Owned values (Address) are removed with their owner; keep them since the owner stays.
                entry.State = EntityState.Unchanged;
            }
        }

    }
}
