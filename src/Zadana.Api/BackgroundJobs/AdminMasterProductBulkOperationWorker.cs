using Zadana.Application.Modules.Catalog.Interfaces;

namespace Zadana.Api.BackgroundJobs;

public sealed class AdminMasterProductBulkOperationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAdminMasterProductBulkOperationQueue _queue;
    private readonly ILogger<AdminMasterProductBulkOperationWorker> _logger;

    public AdminMasterProductBulkOperationWorker(
        IServiceScopeFactory scopeFactory,
        IAdminMasterProductBulkOperationQueue queue,
        ILogger<AdminMasterProductBulkOperationWorker> logger)
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
                _logger.LogError(ex, "Failed to process admin master product bulk operation {OperationId}", operationId);
            }
        }
    }

    private async Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IAdminMasterProductBulkOperationProcessor>();
        await processor.ProcessOperationAsync(operationId, cancellationToken);
    }
}
