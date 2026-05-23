using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Updates audit fields (CreatedAtUtc / UpdatedAtUtc and, when present on
/// derived entities, CreatedById / ModifiedById) on save.
/// We resolve <see cref="ICurrentUserService"/> from the DbContext's scope so
/// audit fields are populated with the active user's id when available.
/// Entities that do not declare CreatedById / ModifiedById are unaffected;
/// reflection-based shadow checks make this opt-in.
/// </summary>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Default constructor used by tests / design-time tooling. CreatedBy /
    /// ModifiedBy enrichment is skipped when no service provider is wired.
    /// </summary>
    public AuditableEntityInterceptor()
    {
        _serviceProvider = null;
    }

    public AuditableEntityInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

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

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null) return;

        var utcNow = DateTime.UtcNow;
        var currentUserId = TryGetCurrentUserId();

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = utcNow;
                    entry.Entity.UpdatedAtUtc = utcNow;
                    SetIfPropertyExists(entry, "CreatedById", currentUserId);
                    SetIfPropertyExists(entry, "ModifiedById", currentUserId);
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = utcNow;
                    // Don't overwrite CreatedAtUtc on update
                    entry.Property(nameof(BaseEntity.CreatedAtUtc)).IsModified = false;

                    // Lock CreatedById on update so it's never reassigned.
                    if (HasProperty(entry, "CreatedById"))
                    {
                        entry.Property("CreatedById").IsModified = false;
                    }

                    SetIfPropertyExists(entry, "ModifiedById", currentUserId);
                    break;
            }
        }
    }

    private Guid? TryGetCurrentUserId()
    {
        if (_serviceProvider is null)
        {
            return null;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var currentUser = scope.ServiceProvider.GetService<ICurrentUserService>();
            return currentUser?.UserId;
        }
        catch
        {
            // Resolving the user can fail during startup or background workers
            // that don't have an HttpContext. Audit silently in those cases.
            return null;
        }
    }

    private static bool HasProperty(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string name) =>
        entry.Metadata.FindProperty(name) is not null;

    private static void SetIfPropertyExists(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        string name,
        Guid? value)
    {
        if (!HasProperty(entry, name))
        {
            return;
        }

        // Don't overwrite an explicit value the caller already set.
        var current = entry.Property(name).CurrentValue;
        if (current is not null)
        {
            return;
        }

        entry.Property(name).CurrentValue = value;
    }
}
