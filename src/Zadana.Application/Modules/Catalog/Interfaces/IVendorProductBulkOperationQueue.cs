namespace Zadana.Application.Modules.Catalog.Interfaces;

public interface IVendorProductBulkOperationQueue
{
    ValueTask EnqueueAsync(Guid operationId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}
