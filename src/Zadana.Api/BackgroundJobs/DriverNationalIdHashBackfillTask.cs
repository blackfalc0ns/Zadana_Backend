using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Identity.Services;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Api.BackgroundJobs;

/// <summary>
/// Idempotent one-shot backfill that computes <c>NationalIdHash</c> for any
/// Driver that has a NationalId but no hash yet. Runs once at startup when
/// <c>Security:RunNationalIdHashBackfill</c> is true. Safe to leave enabled
/// permanently — the WHERE clause skips already-hashed rows so subsequent
/// boots do nothing.
/// 
/// Required because the existing NationalId column is column-level encrypted
/// at rest, so the hash cannot be backfilled via plain SQL — it must flow
/// through EF's value converter to be decrypted first.
/// </summary>
public sealed class DriverNationalIdHashBackfillTask : IHostedService
{
    private const int BatchSize = 200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DriverNationalIdHashBackfillTask> _logger;

    public DriverNationalIdHashBackfillTask(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DriverNationalIdHashBackfillTask> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue<bool>("Security:RunNationalIdHashBackfill"))
        {
            return;
        }

        try
        {
            await BackfillAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Backfill is best-effort — failing here must not block startup.
            _logger.LogError(ex, "DriverNationalIdHashBackfill failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task BackfillAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        int totalUpdated = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await dbContext.Drivers
                .Where(d => d.NationalId != null && d.NationalIdHash == null)
                .OrderBy(d => d.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0) break;

            foreach (var driver in batch)
            {
                var hash = SearchableHashProvider.Compute(driver.NationalId);
                if (string.IsNullOrEmpty(hash)) continue;

                // EF tracks the entity; we only update the shadow property
                // through reflection-free, model-aware Property() to avoid
                // reopening the domain entity for a one-time operation.
                dbContext.Entry(driver).Property("NationalIdHash").CurrentValue = hash;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            totalUpdated += batch.Count;

            // Detach to keep the change tracker small across batches.
            foreach (var driver in batch)
            {
                dbContext.Entry(driver).State = EntityState.Detached;
            }
        }

        if (totalUpdated > 0)
        {
            _logger.LogInformation(
                "DriverNationalIdHashBackfill completed: {Count} rows updated.",
                totalUpdated);
        }
    }
}
