using Zadana.Application.Modules.Catalog.Interfaces;

namespace Zadana.Api.BackgroundJobs;

public sealed class VendorProductBulkOperationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVendorProductBulkOperationQueue _queue;
    private readonly ILogger<VendorProductBulkOperationWorker> _logger;

    public VendorProductBulkOperationWorker(
        IServiceScopeFactory scopeFactory,
        IVendorProductBulkOperationQueue queue,
        ILogger<VendorProductBulkOperationWorker> logger)
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
                _logger.LogError(ex, "Failed to process vendor product bulk operation {OperationId}", operationId);
            }
        }
    }

    private async Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IVendorProductBulkOperationProcessor>();
        await processor.ProcessOperationAsync(operationId, cancellationToken);
    }
}
