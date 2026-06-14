namespace Zadana.Application.Modules.Catalog.Interfaces;

public interface IAdminBrandBulkOperationProcessor
{
    Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
}
