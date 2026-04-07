using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetPendingRequests;
using Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetVendorRequests;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Catalog.Services;

public class ProductRequestReadService : IProductRequestReadService
{
    private readonly ApplicationDbContext _dbContext;

    public ProductRequestReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<AdminProductRequestDto>> GetPendingAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query =
            from productRequest in _dbContext.ProductRequests.AsNoTracking()
            join vendor in _dbContext.Vendors.AsNoTracking() on productRequest.VendorId equals vendor.Id
            join category in _dbContext.Categories.AsNoTracking() on productRequest.SuggestedCategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            where productRequest.Status == ApprovalStatus.Pending
            select new AdminProductRequestDto(
                productRequest.Id,
                productRequest.VendorId,
                vendor.BusinessNameAr,
                productRequest.SuggestedNameAr,
                productRequest.SuggestedNameEn,
                productRequest.SuggestedDescriptionAr,
                productRequest.SuggestedDescriptionEn,
                productRequest.SuggestedCategoryId,
                category != null ? category.NameAr : null,
                category != null ? category.NameEn : null,
                productRequest.ImageUrl,
                productRequest.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<AdminProductRequestDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<PaginatedList<ProductRequestDto>> GetVendorRequestsAsync(
        Guid vendorId,
        ApprovalStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query =
            from productRequest in _dbContext.ProductRequests.AsNoTracking()
            join category in _dbContext.Categories.AsNoTracking() on productRequest.SuggestedCategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            where productRequest.VendorId == vendorId
            select new { productRequest, category };

        if (status.HasValue)
        {
            query = query.Where(item => item.productRequest.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.productRequest.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new ProductRequestDto(
                item.productRequest.Id,
                item.productRequest.SuggestedNameAr,
                item.productRequest.SuggestedNameEn,
                item.productRequest.SuggestedDescriptionAr,
                item.productRequest.SuggestedDescriptionEn,
                item.productRequest.SuggestedCategoryId,
                item.category != null ? item.category.NameAr : null,
                item.category != null ? item.category.NameEn : null,
                item.productRequest.ImageUrl,
                item.productRequest.Status.ToString(),
                item.productRequest.RejectionReason,
                item.productRequest.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PaginatedList<ProductRequestDto>(items, totalCount, pageNumber, pageSize);
    }
}
