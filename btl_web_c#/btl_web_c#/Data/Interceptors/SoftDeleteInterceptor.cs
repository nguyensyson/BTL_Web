using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RestaurantOrdering.Models.Entities;

namespace RestaurantOrdering.Data.Interceptors;

/// <summary>
/// Chặn lệnh Remove() — chuyển thành soft delete (IsDeleted = true).
/// </summary>
public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplySoftDelete(DbContext? context)
    {
        if (context is null) return;

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted && e.Entity is BaseEntity);

        foreach (var entry in entries)
        {
            entry.State = EntityState.Modified;
            ((BaseEntity)entry.Entity).IsDeleted = true;
            ((BaseEntity)entry.Entity).UpdatedTime = DateTime.UtcNow;
        }
    }
}
