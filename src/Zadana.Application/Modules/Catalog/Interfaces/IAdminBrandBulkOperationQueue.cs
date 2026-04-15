namespace Zadana.Application.Modules.Catalog.Interfaces;

public interface IAdminBrandBulkOperationQueue
{
    ValueTask EnqueueAsync(Guid operationId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}
