using Zadana.Application.Modules.Catalog.Interfaces;

namespace Zadana.Api.BackgroundJobs;

public sealed class AdminBrandBulkOperationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAdminBrandBulkOperationQueue _queue;
    private readonly ILogger<AdminBrandBulkOperationWorker> _logger;

    public AdminBrandBulkOperationWorker(
        IServiceScopeFactory scopeFactory,
        IAdminBrandBulkOperationQueue queue,
        ILogger<AdminBrandBulkOperationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var operationId = await _queue.DequeueAsync(stoppingToken);

            try
            {
                await ProcessOperationAsync(operationId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process admin brand bulk operation {OperationId}", operationId);
            }
        }
    }

    private async Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IAdminBrandBulkOperationProcessor>();
        await processor.ProcessOperationAsync(operationId, cancellationToken);
    }
}
