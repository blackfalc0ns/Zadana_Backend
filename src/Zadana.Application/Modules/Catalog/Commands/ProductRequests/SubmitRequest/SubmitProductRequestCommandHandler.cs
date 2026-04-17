using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Common;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.SubmitRequest;

public class SubmitProductRequestCommandHandler : IRequestHandler<SubmitProductRequestCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentVendorService _currentVendorService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SubmitProductRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentVendorService currentVendorService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentVendorService = currentVendorService;
        _localizer = localizer;
    }

    public async Task<Guid> Handle(SubmitProductRequestCommand request, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(cancellationToken)
            ?? throw new ForbiddenAccessException(_localizer["VENDOR_LOGIN_REQUIRED"]);

        if (request.SuggestedCategoryId.HasValue
            && !await _context.Categories.AnyAsync(category => category.Id == request.SuggestedCategoryId.Value, cancellationToken))
        {
            throw new NotFoundException(nameof(Category), request.SuggestedCategoryId.Value);
        }

        if (request.SuggestedBrandId.HasValue
            && !await _context.Brands.AnyAsync(brand => brand.Id == request.SuggestedBrandId.Value, cancellationToken))
        {
            throw new NotFoundException(nameof(Brand), request.SuggestedBrandId.Value);
        }

        if (request.SuggestedCategoryId.HasValue && request.SuggestedBrandId.HasValue)
        {
            var selectedBrandCategoryId = await _context.Brands
                .AsNoTracking()
                .Where(brand => brand.Id == request.SuggestedBrandId.Value)
                .Select(brand => brand.CategoryId)
                .FirstOrDefaultAsync(cancellationToken);

            if (selectedBrandCategoryId.HasValue && selectedBrandCategoryId.Value != request.SuggestedCategoryId.Value)
            {
                throw new BusinessRuleException("BRAND_CATEGORY_MISMATCH", "The selected brand does not belong to the selected category.");
            }
        }

        if (request.SuggestedUnitOfMeasureId.HasValue
            && !await _context.UnitsOfMeasure.AnyAsync(unit => unit.Id == request.SuggestedUnitOfMeasureId.Value, cancellationToken))
        {
            throw new NotFoundException(nameof(UnitOfMeasure), request.SuggestedUnitOfMeasureId.Value);
        }

        BrandRequest? brandRequest = null;
        if (request.RequestedBrand is not null)
        {
            var requestedBrandCategory = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(category => category.Id == request.RequestedBrand.CategoryId, cancellationToken)
                ?? throw new NotFoundException(nameof(Category), request.RequestedBrand.CategoryId);

            if (requestedBrandCategory.ParentCategoryId is null)
            {
                throw new BusinessRuleException("BRAND_CATEGORY_MUST_BE_NESTED", "The selected category is not valid for brand requests.");
            }

            brandRequest = new BrandRequest(
                vendorId,
                request.RequestedBrand.CategoryId,
                request.RequestedBrand.NameAr,
                request.RequestedBrand.NameEn,
                request.RequestedBrand.LogoUrl);

            _context.BrandRequests.Add(brandRequest);
        }

        CategoryRequest? categoryRequest = null;
        if (request.RequestedCategory is not null)
        {
            if (!CategoryHierarchyRules.TryParseTargetLevel(request.RequestedCategory.TargetLevel, out var targetLevel))
            {
                throw new BusinessRuleException("INVALID_CATEGORY_TARGET_LEVEL", "Invalid category target level.");
            }

            if (!CategoryHierarchyRules.IsValidLevel(targetLevel))
            {
                throw new BusinessRuleException("CATEGORY_LEVEL_NOT_SUPPORTED", "Category requests cannot exceed the fourth level.");
            }

            if (!CategoryHierarchyRules.IsRequestTargetLevel(targetLevel))
            {
                throw new BusinessRuleException("CATEGORY_LEVEL_NOT_SUPPORTED", "Only category and sub-category requests are supported.");
            }

            if (!request.RequestedCategory.ParentCategoryId.HasValue)
            {
                throw new BusinessRuleException("CATEGORY_PARENT_REQUIRED", "This category level requires a parent category.");
            }

            if (request.RequestedCategory.ParentCategoryId.HasValue)
            {
                var categories = await _context.Categories
                    .AsNoTracking()
                    .Select(category => new CategoryNode(category.Id, category.ParentCategoryId))
                    .ToListAsync(cancellationToken);

                var lookup = categories.ToDictionary(category => category.Id);

                if (!lookup.TryGetValue(request.RequestedCategory.ParentCategoryId.Value, out var parent))
                {
                    throw new NotFoundException(nameof(Category), request.RequestedCategory.ParentCategoryId.Value);
                }

                var actualParentLevel = ResolveLevel(parent.Id, lookup);
                if (!CategoryHierarchyRules.IsAllowedParentLevel(targetLevel, actualParentLevel))
                {
                    throw new BusinessRuleException("INVALID_CATEGORY_PARENT_LEVEL", "The selected parent category does not match the requested level.");
                }
            }

            categoryRequest = new CategoryRequest(
                vendorId,
                request.RequestedCategory.NameAr,
                request.RequestedCategory.NameEn,
                CategoryHierarchyRules.ToKey(targetLevel),
                request.RequestedCategory.ParentCategoryId,
                request.RequestedCategory.DisplayOrder,
                request.RequestedCategory.ImageUrl);

            _context.CategoryRequests.Add(categoryRequest);
        }

        var productRequest = new ProductRequest(
            vendorId: vendorId,
            suggestedNameAr: request.SuggestedNameAr,
            suggestedNameEn: request.SuggestedNameEn,
            suggestedCategoryId: request.SuggestedCategoryId,
            suggestedCategoryRequestId: categoryRequest?.Id,
            suggestedBrandId: request.SuggestedBrandId,
            suggestedBrandRequestId: brandRequest?.Id,
            suggestedUnitOfMeasureId: request.SuggestedUnitOfMeasureId,
            suggestedDescriptionAr: request.SuggestedDescriptionAr,
            suggestedDescriptionEn: request.SuggestedDescriptionEn,
            imageUrl: request.ImageUrl
        );

        _context.ProductRequests.Add(productRequest);
        await _context.SaveChangesAsync(cancellationToken);

        return productRequest.Id;
    }

    private static int ResolveLevel(Guid categoryId, IReadOnlyDictionary<Guid, CategoryNode> lookup)
    {
        var currentId = categoryId;
        var level = 0;

        while (lookup.TryGetValue(currentId, out var node) && node.ParentCategoryId.HasValue)
        {
            level++;
            currentId = node.ParentCategoryId.Value;
        }

        return level;
    }

    private sealed record CategoryNode(Guid Id, Guid? ParentCategoryId);
}
