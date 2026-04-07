using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Catalog.Services;

public class CatalogRequestReadService : ICatalogRequestReadService
{
    private readonly ApplicationDbContext _dbContext;

    public CatalogRequestReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<CatalogRequestListItemDto>> GetAdminRequestsAsync(
        string? requestType,
        string? status,
        Guid? vendorId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var items = await BuildCombinedRequestsQuery(cancellationToken);
        var filtered = ApplyFilters(items, requestType, status, vendorId);
        var totalCount = filtered.Count;
        var pageItems = filtered
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PaginatedList<CatalogRequestListItemDto>(pageItems, totalCount, pageNumber, pageSize);
    }

    public async Task<CatalogRequestDetailDto?> GetAdminRequestDetailAsync(
        string requestType,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return requestType.ToLowerInvariant() switch
        {
            "product" => await GetProductRequestDetailAsync(requestId, cancellationToken),
            "brand" => await GetBrandRequestDetailAsync(requestId, cancellationToken),
            "category" => await GetCategoryRequestDetailAsync(requestId, cancellationToken),
            _ => null
        };
    }

    public async Task<PaginatedList<CatalogRequestListItemDto>> GetVendorRequestsAsync(
        Guid vendorId,
        string? requestType,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var items = await BuildCombinedRequestsQuery(cancellationToken);
        var filtered = ApplyFilters(items.Where(item => item.VendorId == vendorId).ToList(), requestType, status, null);
        var totalCount = filtered.Count;
        var pageItems = filtered
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PaginatedList<CatalogRequestListItemDto>(pageItems, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<VendorCatalogNotificationDto>> GetVendorNotificationsAsync(
        Guid vendorUserId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications.AsNoTracking()
            .Where(item => item.UserId == vendorUserId && item.Type != null && item.Type.StartsWith("catalog_request"))
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new VendorCatalogNotificationDto(
                item.Id,
                item.Title,
                item.Body,
                item.Type,
                item.IsRead,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<CatalogRequestListItemDto>> BuildCombinedRequestsQuery(CancellationToken cancellationToken)
    {
        var productRequests = await (
            from request in _dbContext.ProductRequests.AsNoTracking()
            join vendor in _dbContext.Vendors.AsNoTracking() on request.VendorId equals vendor.Id
            join category in _dbContext.Categories.AsNoTracking() on request.SuggestedCategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            join categoryRequest in _dbContext.CategoryRequests.AsNoTracking() on request.SuggestedCategoryRequestId equals categoryRequest.Id into categoryRequestJoin
            from categoryRequest in categoryRequestJoin.DefaultIfEmpty()
            join brand in _dbContext.Brands.AsNoTracking() on request.SuggestedBrandId equals brand.Id into brands
            from brand in brands.DefaultIfEmpty()
            join brandRequest in _dbContext.BrandRequests.AsNoTracking() on request.SuggestedBrandRequestId equals brandRequest.Id into brandRequestJoin
            from brandRequest in brandRequestJoin.DefaultIfEmpty()
            join unit in _dbContext.UnitsOfMeasure.AsNoTracking() on request.SuggestedUnitOfMeasureId equals unit.Id into units
            from unit in units.DefaultIfEmpty()
            select new CatalogRequestListItemDto(
                request.Id,
                "product",
                request.VendorId,
                vendor.BusinessNameAr,
                request.SuggestedNameAr,
                request.SuggestedNameEn,
                request.SuggestedDescriptionAr,
                request.SuggestedDescriptionEn,
                request.SuggestedCategoryId ?? categoryRequest.CreatedCategoryId ?? request.SuggestedCategoryRequestId,
                category != null ? category.NameAr : categoryRequest != null ? categoryRequest.NameAr : null,
                category != null ? category.NameEn : categoryRequest != null ? categoryRequest.NameEn : null,
                request.SuggestedBrandId ?? brandRequest.CreatedBrandId ?? request.SuggestedBrandRequestId,
                brand != null ? brand.NameAr : brandRequest != null ? brandRequest.NameAr : null,
                brand != null ? brand.NameEn : brandRequest != null ? brandRequest.NameEn : null,
                null,
                null,
                null,
                request.SuggestedUnitOfMeasureId,
                unit != null ? unit.NameAr : null,
                unit != null ? unit.NameEn : null,
                request.ImageUrl,
                request.Status.ToString(),
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var brandRequests = await (
            from request in _dbContext.BrandRequests.AsNoTracking()
            join vendor in _dbContext.Vendors.AsNoTracking() on request.VendorId equals vendor.Id
            select new CatalogRequestListItemDto(
                request.Id,
                "brand",
                request.VendorId,
                vendor.BusinessNameAr,
                request.NameAr,
                request.NameEn,
                null,
                null,
                null,
                null,
                null,
                request.CreatedBrandId,
                request.NameAr,
                request.NameEn,
                null,
                null,
                null,
                null,
                null,
                null,
                request.LogoUrl,
                request.Status.ToString(),
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var categoryRequests = await (
            from request in _dbContext.CategoryRequests.AsNoTracking()
            join vendor in _dbContext.Vendors.AsNoTracking() on request.VendorId equals vendor.Id
            join parent in _dbContext.Categories.AsNoTracking() on request.ParentCategoryId equals parent.Id into parents
            from parent in parents.DefaultIfEmpty()
            select new CatalogRequestListItemDto(
                request.Id,
                "category",
                request.VendorId,
                vendor.BusinessNameAr,
                request.NameAr,
                request.NameEn,
                null,
                null,
                request.CreatedCategoryId,
                request.NameAr,
                request.NameEn,
                null,
                null,
                null,
                request.ParentCategoryId,
                parent != null ? parent.NameAr : null,
                parent != null ? parent.NameEn : null,
                null,
                null,
                null,
                request.ImageUrl,
                request.Status.ToString(),
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return [.. productRequests, .. brandRequests, .. categoryRequests];
    }

    private static List<CatalogRequestListItemDto> ApplyFilters(
        List<CatalogRequestListItemDto> items,
        string? requestType,
        string? status,
        Guid? vendorId)
    {
        IEnumerable<CatalogRequestListItemDto> query = items;

        if (!string.IsNullOrWhiteSpace(requestType) && requestType != "all")
        {
            query = query.Where(item => string.Equals(item.RequestType, requestType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            query = query.Where(item => string.Equals(item.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (vendorId.HasValue)
        {
            query = query.Where(item => item.VendorId == vendorId.Value);
        }

        return query.ToList();
    }

    private async Task<CatalogRequestDetailDto?> GetProductRequestDetailAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var result = await (
            from request in _dbContext.ProductRequests.AsNoTracking()
            join vendor in _dbContext.Vendors.AsNoTracking() on request.VendorId equals vendor.Id
            join category in _dbContext.Categories.AsNoTracking() on request.SuggestedCategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            join categoryRequest in _dbContext.CategoryRequests.AsNoTracking() on request.SuggestedCategoryRequestId equals categoryRequest.Id into categoryRequestJoin
            from categoryRequest in categoryRequestJoin.DefaultIfEmpty()
            join brand in _dbContext.Brands.AsNoTracking() on request.SuggestedBrandId equals brand.Id into brands
            from brand in brands.DefaultIfEmpty()
            join brandRequest in _dbContext.BrandRequests.AsNoTracking() on request.SuggestedBrandRequestId equals brandRequest.Id into brandRequestJoin
            from brandRequest in brandRequestJoin.DefaultIfEmpty()
            join unit in _dbContext.UnitsOfMeasure.AsNoTracking() on request.SuggestedUnitOfMeasureId equals unit.Id into units
            from unit in units.DefaultIfEmpty()
            where request.Id == requestId
            select new
            {
                request.Id,
                request.VendorId,
                VendorName = vendor.BusinessNameAr,
                request.SuggestedNameAr,
                request.SuggestedNameEn,
                request.SuggestedDescriptionAr,
                request.SuggestedDescriptionEn,
                CategoryId = request.SuggestedCategoryId ?? categoryRequest.CreatedCategoryId ?? request.SuggestedCategoryRequestId,
                CategoryNameAr = category != null ? category.NameAr : categoryRequest != null ? categoryRequest.NameAr : null,
                CategoryNameEn = category != null ? category.NameEn : categoryRequest != null ? categoryRequest.NameEn : null,
                BrandId = request.SuggestedBrandId ?? brandRequest.CreatedBrandId ?? request.SuggestedBrandRequestId,
                BrandNameAr = brand != null ? brand.NameAr : brandRequest != null ? brandRequest.NameAr : null,
                BrandNameEn = brand != null ? brand.NameEn : brandRequest != null ? brandRequest.NameEn : null,
                ParentCategoryId = categoryRequest != null ? categoryRequest.ParentCategoryId : null,
                UnitId = request.SuggestedUnitOfMeasureId,
                UnitNameAr = unit != null ? unit.NameAr : null,
                UnitNameEn = unit != null ? unit.NameEn : null,
                request.ImageUrl,
                Status = request.Status.ToString(),
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedMasterProductId,
                request.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return result is null
            ? null
            : new CatalogRequestDetailDto(
                result.Id,
                "product",
                result.VendorId,
                result.VendorName,
                result.SuggestedNameAr,
                result.SuggestedNameEn,
                result.SuggestedDescriptionAr,
                result.SuggestedDescriptionEn,
                result.CategoryId,
                result.CategoryNameAr,
                result.CategoryNameEn,
                result.BrandId,
                result.BrandNameAr,
                result.BrandNameEn,
                result.ParentCategoryId,
                null,
                null,
                null,
                result.UnitId,
                result.UnitNameAr,
                result.UnitNameEn,
                result.ImageUrl,
                result.Status,
                result.RejectionReason,
                result.ReviewedBy,
                result.ReviewedAtUtc,
                result.CreatedMasterProductId,
                result.CreatedMasterProductId.HasValue ? "product" : null,
                result.CreatedAtUtc);
    }

    private async Task<CatalogRequestDetailDto?> GetBrandRequestDetailAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var result = await (
            from request in _dbContext.BrandRequests.AsNoTracking()
            join vendor in _dbContext.Vendors.AsNoTracking() on request.VendorId equals vendor.Id
            where request.Id == requestId
            select new
            {
                request.Id,
                request.VendorId,
                VendorName = vendor.BusinessNameAr,
                request.NameAr,
                request.NameEn,
                request.LogoUrl,
                Status = request.Status.ToString(),
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedBrandId,
                request.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return result is null
            ? null
            : new CatalogRequestDetailDto(
                result.Id,
                "brand",
                result.VendorId,
                result.VendorName,
                result.NameAr,
                result.NameEn,
                null,
                null,
                null,
                null,
                null,
                result.CreatedBrandId,
                result.NameAr,
                result.NameEn,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                result.LogoUrl,
                result.Status,
                result.RejectionReason,
                result.ReviewedBy,
                result.ReviewedAtUtc,
                result.CreatedBrandId,
                result.CreatedBrandId.HasValue ? "brand" : null,
                result.CreatedAtUtc);
    }

    private async Task<CatalogRequestDetailDto?> GetCategoryRequestDetailAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var result = await (
            from request in _dbContext.CategoryRequests.AsNoTracking()
            join vendor in _dbContext.Vendors.AsNoTracking() on request.VendorId equals vendor.Id
            join parent in _dbContext.Categories.AsNoTracking() on request.ParentCategoryId equals parent.Id into parents
            from parent in parents.DefaultIfEmpty()
            where request.Id == requestId
            select new
            {
                request.Id,
                request.VendorId,
                VendorName = vendor.BusinessNameAr,
                request.NameAr,
                request.NameEn,
                request.ParentCategoryId,
                ParentCategoryNameAr = parent != null ? parent.NameAr : null,
                ParentCategoryNameEn = parent != null ? parent.NameEn : null,
                request.DisplayOrder,
                request.ImageUrl,
                Status = request.Status.ToString(),
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedCategoryId,
                request.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return result is null
            ? null
            : new CatalogRequestDetailDto(
                result.Id,
                "category",
                result.VendorId,
                result.VendorName,
                result.NameAr,
                result.NameEn,
                null,
                null,
                result.CreatedCategoryId,
                result.NameAr,
                result.NameEn,
                null,
                null,
                null,
                result.ParentCategoryId,
                result.ParentCategoryNameAr,
                result.ParentCategoryNameEn,
                result.DisplayOrder,
                null,
                null,
                null,
                result.ImageUrl,
                result.Status,
                result.RejectionReason,
                result.ReviewedBy,
                result.ReviewedAtUtc,
                result.CreatedCategoryId,
                result.CreatedCategoryId.HasValue ? "category" : null,
                result.CreatedAtUtc);
    }
}
