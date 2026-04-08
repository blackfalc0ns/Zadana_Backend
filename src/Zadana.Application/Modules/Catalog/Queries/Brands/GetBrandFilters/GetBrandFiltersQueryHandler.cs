using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Brands;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandFilters;

public class GetBrandFiltersQueryHandler : IRequestHandler<GetBrandFiltersQuery, BrandFiltersDto>
{
    private readonly IApplicationDbContext _context;

    public GetBrandFiltersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BrandFiltersDto> Handle(GetBrandFiltersQuery request, CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .AsNoTracking()
            .Where(item => item.Id == request.BrandId && item.IsActive)
            .Select(item => new { item.Id, item.NameAr, item.NameEn })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Brand), request.BrandId);

        var categories = await _context.Categories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .Select(category => new CategoryRow(
                category.Id,
                category.ParentCategoryId,
                category.NameAr,
                category.NameEn,
                category.DisplayOrder))
            .ToListAsync(cancellationToken);

        var categoriesById = categories.ToDictionary(category => category.Id);

        var scopedMasterProducts = await _context.MasterProducts
            .AsNoTracking()
            .Where(product =>
                product.Status == ProductStatus.Active &&
                product.BrandId == request.BrandId)
            .Select(product => new ScopedMasterProductRow(
                product.CategoryId,
                product.UnitOfMeasureId))
            .ToListAsync(cancellationToken);

        var activeCategoryIds = scopedMasterProducts
            .Select(product => product.CategoryId)
            .Where(categoriesById.ContainsKey)
            .Distinct()
            .ToList();

        var unitsIds = scopedMasterProducts
            .Where(product => product.UnitOfMeasureId.HasValue)
            .Select(product => product.UnitOfMeasureId!.Value)
            .Distinct()
            .ToList();

        var categoryItems = new Dictionary<Guid, CatalogFilterNamedItemDto>();
        var subcategoryItems = new Dictionary<Guid, BrandFilterSubcategoryItemDto>();

        foreach (var categoryId in activeCategoryIds)
        {
            var category = categoriesById[categoryId];

            if (category.ParentCategoryId.HasValue && categoriesById.TryGetValue(category.ParentCategoryId.Value, out var parent))
            {
                categoryItems[parent.Id] = new CatalogFilterNamedItemDto(
                    parent.Id,
                    BrandCatalogQueryHelpers.PickLocalized(parent.NameAr, parent.NameEn));

                subcategoryItems[category.Id] = new BrandFilterSubcategoryItemDto(
                    category.Id,
                    BrandCatalogQueryHelpers.PickLocalized(category.NameAr, category.NameEn),
                    parent.Id);
            }
            else
            {
                categoryItems[category.Id] = new CatalogFilterNamedItemDto(
                    category.Id,
                    BrandCatalogQueryHelpers.PickLocalized(category.NameAr, category.NameEn));
            }
        }

        var unitRows = await _context.UnitsOfMeasure
            .AsNoTracking()
            .Where(unit => unit.IsActive && unitsIds.Contains(unit.Id))
            .Select(unit => new UnitRow(
                unit.Id,
                unit.NameAr,
                unit.NameEn))
            .ToListAsync(cancellationToken);

        var units = unitRows
            .Select(unit => new CatalogFilterNamedItemDto(
                unit.Id,
                BrandCatalogQueryHelpers.PickLocalized(unit.NameAr, unit.NameEn)))
            .OrderBy(unit => unit.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var visiblePrices = await _context.VendorProducts
            .AsNoTracking()
            .Where(product =>
                product.Status == VendorProductStatus.Active &&
                product.IsAvailable &&
                product.StockQuantity > 0 &&
                product.MasterProduct.Status == ProductStatus.Active &&
                product.MasterProduct.BrandId == request.BrandId &&
                product.Vendor.Status == VendorStatus.Active &&
                product.Vendor.AcceptOrders)
            .Select(product => product.SellingPrice)
            .ToListAsync(cancellationToken);

        var priceRange = visiblePrices.Count == 0
            ? new CatalogFilterPriceRangeDto(0, 0)
            : new CatalogFilterPriceRangeDto(visiblePrices.Min(), visiblePrices.Max());

        return new BrandFiltersDto(
            new CatalogFilterNamedItemDto(
                brand.Id,
                BrandCatalogQueryHelpers.PickLocalized(brand.NameAr, brand.NameEn)),
            categoryItems.Values
                .OrderBy(item => categoriesById[item.Id].DisplayOrder)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            subcategoryItems.Values
                .OrderBy(item => categoriesById[item.CategoryId].DisplayOrder)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            units,
            priceRange,
            BrandCatalogQueryHelpers.BuildSortOptions());
    }

    private sealed record CategoryRow(
        Guid Id,
        Guid? ParentCategoryId,
        string? NameAr,
        string? NameEn,
        int DisplayOrder);

    private sealed record ScopedMasterProductRow(
        Guid CategoryId,
        Guid? UnitOfMeasureId);

    private sealed record UnitRow(
        Guid Id,
        string? NameAr,
        string? NameEn);
}
