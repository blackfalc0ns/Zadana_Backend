using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Infrastructure.Persistence.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    // TODO: Inject ICurrentUserService once auth is implemented
    // private readonly ICurrentUserService _currentUserService;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void UpdateAuditFields(DbContext? context)
    {
        if (context is null) return;

        var utcNow = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = utcNow;
                    entry.Entity.UpdatedAtUtc = utcNow;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = utcNow;
                    // Don't overwrite CreatedAtUtc on update
                    entry.Property(nameof(BaseEntity.CreatedAtUtc)).IsModified = false;
                    break;
            }
        }
    }
}
