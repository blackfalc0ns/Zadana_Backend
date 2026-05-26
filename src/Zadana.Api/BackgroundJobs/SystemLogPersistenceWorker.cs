using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.BackgroundJobs;

/// <summary>
/// Drains the <see cref="ISystemLogQueue"/> in batches and writes them to the
/// database. Batching reduces round-trips dramatically under load: 1000
/// requests/sec become ~10 INSERT batches/sec instead of 1000 INSERTs.
/// </summary>
public sealed class SystemLogPersistenceWorker : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

    private readonly ISystemLogQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SystemLogPersistenceWorker> _logger;

    public SystemLogPersistenceWorker(
        ISystemLogQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<SystemLogPersistenceWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var buffer = new List<SystemLogEntry>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            buffer.Clear();

            try
            {
                // Wait for at least one item, then drain up to BatchSize.
                var first = await _queue.Reader.ReadAsync(stoppingToken);
                buffer.Add(first);

                using var flushCts = new CancellationTokenSource(FlushInterval);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, flushCts.Token);

                while (buffer.Count < BatchSize &&
                       _queue.Reader.TryRead(out var next))
                {
                    buffer.Add(next);
                }

                await PersistBatchAsync(buffer, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                if (buffer.Count > 0)
                {
                    try { await PersistBatchAsync(buffer, CancellationToken.None); }
                    catch (Exception flushEx) { _logger.LogWarning(flushEx, "Final SystemLog flush failed."); }
                }
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to flush SystemLog batch ({Count} entries).", buffer.Count);
                // Brief back-off so a hot DB doesn't trigger a tight loop.
                try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task PersistBatchAsync(List<SystemLogEntry> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // ChangeTracker.AutoDetectChanges = false is a measurable win when
        // adding hundreds of entities at once; we save once and dispose.
        var prevAutoDetect = dbContext.ChangeTracker.AutoDetectChangesEnabled;
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            await dbContext.SystemLogEntries.AddRangeAsync(batch, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = prevAutoDetect;
        }
    }
}
