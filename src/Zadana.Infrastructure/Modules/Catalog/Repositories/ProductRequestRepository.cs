using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Catalog.Repositories;

public class ProductRequestRepository : IProductRequestRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ProductRequestRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        _dbContext.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken);

    public Task<ProductRequest?> GetByIdAsync(Guid productRequestId, CancellationToken cancellationToken = default) =>
        _dbContext.ProductRequests.FirstOrDefaultAsync(request => request.Id == productRequestId, cancellationToken);

    public void Add(ProductRequest productRequest) => _dbContext.ProductRequests.Add(productRequest);

    public void AddMasterProduct(MasterProduct masterProduct) => _dbContext.MasterProducts.Add(masterProduct);
}
