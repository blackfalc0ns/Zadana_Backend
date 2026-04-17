using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.Common;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Interfaces;
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
        var categoryLookup = await LoadCategoryLookupAsync(cancellationToken);

        return requestType.ToLowerInvariant() switch
        {
            "product" => await GetProductRequestDetailAsync(requestId, categoryLookup, cancellationToken),
            "brand" => await GetBrandRequestDetailAsync(requestId, categoryLookup, cancellationToken),
            "category" => await GetCategoryRequestDetailAsync(requestId, categoryLookup, cancellationToken),
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
        var categoryLookup = await LoadCategoryLookupAsync(cancellationToken);

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
            select new ProductRequestRow(
                request.Id,
                request.VendorId,
                vendor.BusinessNameAr,
                request.SuggestedNameAr,
                request.SuggestedNameEn,
                request.SuggestedDescriptionAr,
                request.SuggestedDescriptionEn,
                request.SuggestedCategoryId,
                category != null ? category.NameAr : null,
                category != null ? category.NameEn : null,
                request.SuggestedCategoryRequestId,
                categoryRequest != null ? categoryRequest.NameAr : null,
                categoryRequest != null ? categoryRequest.NameEn : null,
                categoryRequest != null ? categoryRequest.ParentCategoryId : null,
                categoryRequest != null ? categoryRequest.CreatedCategoryId : null,
                request.SuggestedBrandId,
                brand != null ? brand.NameAr : brandRequest != null ? brandRequest.NameAr : null,
                brand != null ? brand.NameEn : brandRequest != null ? brandRequest.NameEn : null,
                request.SuggestedUnitOfMeasureId,
                unit != null ? unit.NameAr : null,
                unit != null ? unit.NameEn : null,
                request.ImageUrl,
                request.Status.ToString(),
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedAtUtc,
                null,
                categoryRequest != null ? categoryRequest.TargetLevel : null))
            .ToListAsync(cancellationToken);

        var brandRequests = await (
            from request in _dbContext.BrandRequests.AsNoTracking()
            join vendor in _dbContext.Vendors.AsNoTracking() on request.VendorId equals vendor.Id
            join category in _dbContext.Categories.AsNoTracking() on request.CategoryId equals category.Id
            select new
            {
                request.Id,
                request.VendorId,
                VendorName = vendor.BusinessNameAr,
                request.NameAr,
                request.NameEn,
                request.CategoryId,
                CategoryNameAr = category.NameAr,
                CategoryNameEn = category.NameEn,
                category.ParentCategoryId,
                request.CreatedBrandId,
                request.LogoUrl,
                Status = request.Status.ToString(),
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var categoryRequests = await (
            from request in _dbContext.CategoryRequests.AsNoTracking()
            join vendor in _dbContext.Vendors.AsNoTracking() on request.VendorId equals vendor.Id
            join parent in _dbContext.Categories.AsNoTracking() on request.ParentCategoryId equals parent.Id into parents
            from parent in parents.DefaultIfEmpty()
            select new CategoryRequestRow(
                request.Id,
                request.VendorId,
                vendor.BusinessNameAr,
                request.NameAr,
                request.NameEn,
                request.ParentCategoryId,
                parent != null ? parent.NameAr : null,
                parent != null ? parent.NameEn : null,
                request.DisplayOrder,
                request.ImageUrl,
                request.Status.ToString(),
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedCategoryId,
                request.CreatedAtUtc,
                request.TargetLevel))
            .ToListAsync(cancellationToken);

        var mappedProductRequests = productRequests.Select(request =>
        {
            var requestedLevelKey = request.CategoryRequestId.HasValue
                ? request.CategoryRequestTargetLevel
                : null;
            var requestedPathAr = request.CategoryRequestId.HasValue
                ? BuildRequestedPath(request.CategoryRequestParentId, request.CategoryRequestNameAr, categoryLookup, true)
                : null;
            var requestedPathEn = request.CategoryRequestId.HasValue
                ? BuildRequestedPath(request.CategoryRequestParentId, request.CategoryRequestNameEn, categoryLookup, false)
                : null;

            var approvedCategoryId = request.CategoryId ?? request.CreatedCategoryId;
            var approvedPathAr = approvedCategoryId.HasValue ? BuildExistingPath(approvedCategoryId.Value, categoryLookup, true) : null;
            var approvedPathEn = approvedCategoryId.HasValue ? BuildExistingPath(approvedCategoryId.Value, categoryLookup, false) : null;

            return new CatalogRequestListItemDto(
                request.Id,
                "product",
                request.VendorId,
                request.VendorName,
                request.NameAr,
                request.NameEn,
                request.DescriptionAr,
                request.DescriptionEn,
                approvedCategoryId ?? request.CategoryRequestId,
                request.CategoryNameAr ?? request.CategoryRequestNameAr,
                request.CategoryNameEn ?? request.CategoryRequestNameEn,
                request.BrandId,
                request.BrandNameAr,
                request.BrandNameEn,
                request.CategoryRequestParentId,
                request.CategoryRequestParentId.HasValue && categoryLookup.TryGetValue(request.CategoryRequestParentId.Value, out var parent)
                    ? parent.NameAr
                    : null,
                request.CategoryRequestParentId.HasValue && categoryLookup.TryGetValue(request.CategoryRequestParentId.Value, out var parentEn)
                    ? parentEn.NameEn
                    : null,
                request.UnitId,
                request.UnitNameAr,
                request.UnitNameEn,
                request.ImageUrl,
                request.Status,
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedAtUtc,
                requestedLevelKey is null ? null : ResolveRequestKind(requestedLevelKey),
                requestedLevelKey,
                requestedPathAr,
                requestedPathEn,
                approvedPathAr,
                approvedPathEn);
        }).ToList();

        var mappedCategoryRequests = categoryRequests.Select(request => new CatalogRequestListItemDto(
            request.Id,
            "category",
            request.VendorId,
            request.VendorName,
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
            request.ParentCategoryNameAr,
            request.ParentCategoryNameEn,
            null,
            null,
            null,
            request.ImageUrl,
            request.Status,
            request.RejectionReason,
            request.ReviewedBy,
            request.ReviewedAtUtc,
            request.CreatedAtUtc,
            ResolveRequestKind(request.TargetLevel),
            request.TargetLevel,
            BuildRequestedPath(request.ParentCategoryId, request.NameAr, categoryLookup, true),
            BuildRequestedPath(request.ParentCategoryId, request.NameEn, categoryLookup, false),
            request.CreatedCategoryId.HasValue ? BuildExistingPath(request.CreatedCategoryId.Value, categoryLookup, true) : null,
            request.CreatedCategoryId.HasValue ? BuildExistingPath(request.CreatedCategoryId.Value, categoryLookup, false) : null))
            .ToList();

        var mappedBrandRequests = brandRequests.Select(request => new CatalogRequestListItemDto(
            request.Id,
            "brand",
            request.VendorId,
            request.VendorName,
            request.NameAr,
            request.NameEn,
            null,
            null,
            request.CategoryId,
            request.CategoryNameAr,
            request.CategoryNameEn,
            request.CreatedBrandId,
            request.NameAr,
            request.NameEn,
            request.ParentCategoryId,
            request.ParentCategoryId.HasValue && categoryLookup.TryGetValue(request.ParentCategoryId.Value, out var parentAr)
                ? parentAr.NameAr
                : null,
            request.ParentCategoryId.HasValue && categoryLookup.TryGetValue(request.ParentCategoryId.Value, out var parentEn)
                ? parentEn.NameEn
                : null,
            null,
            null,
            null,
            request.LogoUrl,
            request.Status,
            request.RejectionReason,
            request.ReviewedBy,
            request.ReviewedAtUtc,
            request.CreatedAtUtc,
            null,
            null,
            BuildExistingPath(request.CategoryId, categoryLookup, true),
            BuildExistingPath(request.CategoryId, categoryLookup, false),
            request.CreatedBrandId.HasValue ? BuildExistingPath(request.CategoryId, categoryLookup, true) : null,
            request.CreatedBrandId.HasValue ? BuildExistingPath(request.CategoryId, categoryLookup, false) : null))
            .ToList();

        return [.. mappedProductRequests, .. mappedBrandRequests, .. mappedCategoryRequests];
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

    private async Task<CatalogRequestDetailDto?> GetProductRequestDetailAsync(
        Guid requestId,
        IReadOnlyDictionary<Guid, CategoryNode> categoryLookup,
        CancellationToken cancellationToken)
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
            select new ProductRequestRow(
                request.Id,
                request.VendorId,
                vendor.BusinessNameAr,
                request.SuggestedNameAr,
                request.SuggestedNameEn,
                request.SuggestedDescriptionAr,
                request.SuggestedDescriptionEn,
                request.SuggestedCategoryId,
                category != null ? category.NameAr : null,
                category != null ? category.NameEn : null,
                request.SuggestedCategoryRequestId,
                categoryRequest != null ? categoryRequest.NameAr : null,
                categoryRequest != null ? categoryRequest.NameEn : null,
                categoryRequest != null ? categoryRequest.ParentCategoryId : null,
                categoryRequest != null ? categoryRequest.CreatedCategoryId : null,
                request.SuggestedBrandId,
                brand != null ? brand.NameAr : brandRequest != null ? brandRequest.NameAr : null,
                brand != null ? brand.NameEn : brandRequest != null ? brandRequest.NameEn : null,
                request.SuggestedUnitOfMeasureId,
                unit != null ? unit.NameAr : null,
                unit != null ? unit.NameEn : null,
                request.ImageUrl,
                request.Status.ToString(),
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedAtUtc,
                request.CreatedMasterProductId,
                categoryRequest != null ? categoryRequest.TargetLevel : null))
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return null;
        }

        var requestedLevelKey = result.CategoryRequestId.HasValue
            ? result.CategoryRequestTargetLevel
            : null;
        var requestedPathAr = result.CategoryRequestId.HasValue
            ? BuildRequestedPath(result.CategoryRequestParentId, result.CategoryRequestNameAr, categoryLookup, true)
            : null;
        var requestedPathEn = result.CategoryRequestId.HasValue
            ? BuildRequestedPath(result.CategoryRequestParentId, result.CategoryRequestNameEn, categoryLookup, false)
            : null;
        var approvedCategoryId = result.CategoryId ?? result.CreatedCategoryId;

        return new CatalogRequestDetailDto(
            result.Id,
            "product",
            result.VendorId,
            result.VendorName,
            result.NameAr,
            result.NameEn,
            result.DescriptionAr,
            result.DescriptionEn,
            approvedCategoryId ?? result.CategoryRequestId,
            result.CategoryNameAr ?? result.CategoryRequestNameAr,
            result.CategoryNameEn ?? result.CategoryRequestNameEn,
            result.BrandId,
            result.BrandNameAr,
            result.BrandNameEn,
            result.CategoryRequestParentId,
            result.CategoryRequestParentId.HasValue && categoryLookup.TryGetValue(result.CategoryRequestParentId.Value, out var parentAr)
                ? parentAr.NameAr
                : null,
            result.CategoryRequestParentId.HasValue && categoryLookup.TryGetValue(result.CategoryRequestParentId.Value, out var parentEn)
                ? parentEn.NameEn
                : null,
            null,
            result.UnitId,
            result.UnitNameAr,
            result.UnitNameEn,
            result.ImageUrl,
            result.Status,
            result.RejectionReason,
            result.ReviewedBy,
            result.ReviewedAtUtc,
            result.CreatedEntityId,
            result.CreatedEntityId.HasValue ? "product" : null,
            result.CreatedAtUtc,
            requestedLevelKey is null ? null : ResolveRequestKind(requestedLevelKey),
            requestedLevelKey,
            requestedPathAr,
            requestedPathEn,
            approvedCategoryId.HasValue ? BuildExistingPath(approvedCategoryId.Value, categoryLookup, true) : null,
            approvedCategoryId.HasValue ? BuildExistingPath(approvedCategoryId.Value, categoryLookup, false) : null);
    }

    private async Task<CatalogRequestDetailDto?> GetBrandRequestDetailAsync(
        Guid requestId,
        IReadOnlyDictionary<Guid, CategoryNode> categoryLookup,
        CancellationToken cancellationToken)
    {
        var result = await (
            from request in _dbContext.BrandRequests.AsNoTracking()
            join vendor in _dbContext.Vendors.AsNoTracking() on request.VendorId equals vendor.Id
            join category in _dbContext.Categories.AsNoTracking() on request.CategoryId equals category.Id
            where request.Id == requestId
            select new
            {
                request.Id,
                request.VendorId,
                VendorName = vendor.BusinessNameAr,
                request.NameAr,
                request.NameEn,
                request.CategoryId,
                CategoryNameAr = category.NameAr,
                CategoryNameEn = category.NameEn,
                category.ParentCategoryId,
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
                Id: result.Id,
                RequestType: "brand",
                VendorId: result.VendorId,
                VendorName: result.VendorName,
                NameAr: result.NameAr,
                NameEn: result.NameEn,
                DescriptionAr: null,
                DescriptionEn: null,
                CategoryId: result.CategoryId,
                CategoryNameAr: result.CategoryNameAr,
                CategoryNameEn: result.CategoryNameEn,
                BrandId: result.CreatedBrandId,
                BrandNameAr: result.NameAr,
                BrandNameEn: result.NameEn,
                ParentCategoryId: result.ParentCategoryId,
                ParentCategoryNameAr: result.ParentCategoryId.HasValue && categoryLookup.TryGetValue(result.ParentCategoryId.Value, out var parentAr)
                    ? parentAr.NameAr
                    : null,
                ParentCategoryNameEn: result.ParentCategoryId.HasValue && categoryLookup.TryGetValue(result.ParentCategoryId.Value, out var parentEn)
                    ? parentEn.NameEn
                    : null,
                DisplayOrder: null,
                UnitId: null,
                UnitNameAr: null,
                UnitNameEn: null,
                ImageUrl: result.LogoUrl,
                Status: result.Status,
                RejectionReason: result.RejectionReason,
                ReviewedBy: result.ReviewedBy,
                ReviewedAtUtc: result.ReviewedAtUtc,
                CreatedEntityId: result.CreatedBrandId,
                CreatedEntityType: result.CreatedBrandId.HasValue ? "brand" : null,
                CreatedAtUtc: result.CreatedAtUtc,
                RequestKind: null,
                RequestedLevelKey: null,
                RequestedPathAr: BuildExistingPath(result.CategoryId, categoryLookup, true),
                RequestedPathEn: BuildExistingPath(result.CategoryId, categoryLookup, false),
                ApprovedPathAr: result.CreatedBrandId.HasValue ? BuildExistingPath(result.CategoryId, categoryLookup, true) : null,
                ApprovedPathEn: result.CreatedBrandId.HasValue ? BuildExistingPath(result.CategoryId, categoryLookup, false) : null);
    }

    private async Task<CatalogRequestDetailDto?> GetCategoryRequestDetailAsync(
        Guid requestId,
        IReadOnlyDictionary<Guid, CategoryNode> categoryLookup,
        CancellationToken cancellationToken)
    {
        var result = await (
            from request in _dbContext.CategoryRequests.AsNoTracking()
            join vendor in _dbContext.Vendors.AsNoTracking() on request.VendorId equals vendor.Id
            join parent in _dbContext.Categories.AsNoTracking() on request.ParentCategoryId equals parent.Id into parents
            from parent in parents.DefaultIfEmpty()
            where request.Id == requestId
            select new CategoryRequestRow(
                request.Id,
                request.VendorId,
                vendor.BusinessNameAr,
                request.NameAr,
                request.NameEn,
                request.ParentCategoryId,
                parent != null ? parent.NameAr : null,
                parent != null ? parent.NameEn : null,
                request.DisplayOrder,
                request.ImageUrl,
                request.Status.ToString(),
                request.RejectionReason,
                request.ReviewedBy,
                request.ReviewedAtUtc,
                request.CreatedCategoryId,
                request.CreatedAtUtc,
                request.TargetLevel))
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
                result.CreatedAtUtc,
                ResolveRequestKind(result.TargetLevel),
                result.TargetLevel,
                BuildRequestedPath(result.ParentCategoryId, result.NameAr, categoryLookup, true),
                BuildRequestedPath(result.ParentCategoryId, result.NameEn, categoryLookup, false),
                result.CreatedCategoryId.HasValue ? BuildExistingPath(result.CreatedCategoryId.Value, categoryLookup, true) : null,
                result.CreatedCategoryId.HasValue ? BuildExistingPath(result.CreatedCategoryId.Value, categoryLookup, false) : null);
    }

    private async Task<Dictionary<Guid, CategoryNode>> LoadCategoryLookupAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Categories.AsNoTracking()
            .Select(category => new CategoryNode(
                category.Id,
                category.ParentCategoryId,
                category.NameAr,
                category.NameEn))
            .ToDictionaryAsync(category => category.Id, cancellationToken);
    }

    private static string? ResolveRequestedLevelKey(Guid? parentCategoryId, IReadOnlyDictionary<Guid, CategoryNode> categoryLookup)
    {
        var targetLevel = parentCategoryId.HasValue
            ? ResolveLevel(parentCategoryId.Value, categoryLookup) + 1
            : CategoryHierarchyRules.ActivityLevel;

        return CategoryHierarchyRules.IsValidLevel(targetLevel)
            ? CategoryHierarchyRules.ToKey(targetLevel)
            : null;
    }

    private static string? ResolveRequestKind(string? levelKey) =>
        string.IsNullOrWhiteSpace(levelKey)
            ? null
            : string.Equals(levelKey, "sub_category", StringComparison.OrdinalIgnoreCase)
                ? "sub_category"
                : "category";

    private static int ResolveLevel(Guid categoryId, IReadOnlyDictionary<Guid, CategoryNode> categoryLookup)
    {
        var level = 0;
        var currentId = categoryId;

        while (categoryLookup.TryGetValue(currentId, out var category) && category.ParentCategoryId.HasValue)
        {
            level++;
            currentId = category.ParentCategoryId.Value;
        }

        return level;
    }

    private static string? BuildRequestedPath(
        Guid? parentCategoryId,
        string? name,
        IReadOnlyDictionary<Guid, CategoryNode> categoryLookup,
        bool useArabic)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var segments = parentCategoryId.HasValue
            ? BuildExistingSegments(parentCategoryId.Value, categoryLookup, useArabic)
            : new List<string>();

        segments.Add(name);
        return string.Join(" > ", segments);
    }

    private static string? BuildExistingPath(Guid categoryId, IReadOnlyDictionary<Guid, CategoryNode> categoryLookup, bool useArabic)
    {
        var segments = BuildExistingSegments(categoryId, categoryLookup, useArabic);
        return segments.Count == 0 ? null : string.Join(" > ", segments);
    }

    private static List<string> BuildExistingSegments(Guid categoryId, IReadOnlyDictionary<Guid, CategoryNode> categoryLookup, bool useArabic)
    {
        var segments = new List<string>();
        var currentId = categoryId;

        while (categoryLookup.TryGetValue(currentId, out var category))
        {
            segments.Insert(0, useArabic ? category.NameAr : category.NameEn);

            if (!category.ParentCategoryId.HasValue)
            {
                break;
            }

            currentId = category.ParentCategoryId.Value;
        }

        return segments;
    }

    private sealed record CategoryNode(Guid Id, Guid? ParentCategoryId, string NameAr, string NameEn);

    private sealed record ProductRequestRow(
        Guid Id,
        Guid VendorId,
        string VendorName,
        string NameAr,
        string NameEn,
        string? DescriptionAr,
        string? DescriptionEn,
        Guid? CategoryId,
        string? CategoryNameAr,
        string? CategoryNameEn,
        Guid? CategoryRequestId,
        string? CategoryRequestNameAr,
        string? CategoryRequestNameEn,
        Guid? CategoryRequestParentId,
        Guid? CreatedCategoryId,
        Guid? BrandId,
        string? BrandNameAr,
        string? BrandNameEn,
        Guid? UnitId,
        string? UnitNameAr,
        string? UnitNameEn,
        string? ImageUrl,
        string Status,
        string? RejectionReason,
        string? ReviewedBy,
        DateTime? ReviewedAtUtc,
        DateTime CreatedAtUtc,
        Guid? CreatedEntityId = null,
        string? CategoryRequestTargetLevel = null);

    private sealed record CategoryRequestRow(
        Guid Id,
        Guid VendorId,
        string VendorName,
        string NameAr,
        string NameEn,
        Guid? ParentCategoryId,
        string? ParentCategoryNameAr,
        string? ParentCategoryNameEn,
        int? DisplayOrder,
        string? ImageUrl,
        string Status,
        string? RejectionReason,
        string? ReviewedBy,
        DateTime? ReviewedAtUtc,
        Guid? CreatedCategoryId,
        DateTime CreatedAtUtc,
        string TargetLevel);
}
