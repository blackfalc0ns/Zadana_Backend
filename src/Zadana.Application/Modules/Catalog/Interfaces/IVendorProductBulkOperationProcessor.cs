namespace Zadana.Application.Modules.Catalog.Interfaces;

public interface IVendorProductBulkOperationProcessor
{
    Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
}
