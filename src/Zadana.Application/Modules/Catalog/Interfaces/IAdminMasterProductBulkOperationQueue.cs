namespace Zadana.Application.Modules.Catalog.Interfaces;

public interface IAdminMasterProductBulkOperationQueue
{
    ValueTask EnqueueAsync(Guid operationId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}
