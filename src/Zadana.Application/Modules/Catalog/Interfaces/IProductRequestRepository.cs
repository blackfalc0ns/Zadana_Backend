using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Catalog.Interfaces;

public interface IProductRequestRepository
{
    Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<ProductRequest?> GetByIdAsync(Guid productRequestId, CancellationToken cancellationToken = default);
    void Add(ProductRequest productRequest);
    void AddMasterProduct(MasterProduct masterProduct);
}
