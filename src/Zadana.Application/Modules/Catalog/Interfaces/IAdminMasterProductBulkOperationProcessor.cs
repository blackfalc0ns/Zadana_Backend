namespace Zadana.Application.Modules.Catalog.Interfaces;

public interface IAdminMasterProductBulkOperationProcessor
{
    Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
}
