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
    private readonly IAdminAlertService _adminAlertService;

    public SubmitProductRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentVendorService currentVendorService,
        IStringLocalizer<SharedResource> localizer,
        IAdminAlertService adminAlertService)
    {
        _context = context;
        _currentVendorService = currentVendorService;
        _localizer = localizer;
        _adminAlertService = adminAlertService;
    }

    public async Task<Guid> Handle(SubmitProductRequestCommand request, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("VENDOR_LOGIN_REQUIRED");

        if (request.SuggestedBrandId.HasValue && request.RequestedBrand is not null)
        {
            throw new BusinessRuleException(
                "PRODUCT_REQUEST_BRAND_CONFLICT",
                "Choose either an existing brand or a new brand request, not both.");
        }

        if (request.SuggestedCategoryId.HasValue && request.RequestedCategory is not null)
        {
            throw new BusinessRuleException(
                "PRODUCT_REQUEST_CATEGORY_CONFLICT",
                "Choose either an existing category or a new category request, not both.");
        }

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
            var selectedBrand = await _context.Brands
                .AsNoTracking()
                .Include(brand => brand.BrandCategories)
                .FirstOrDefaultAsync(brand => brand.Id == request.SuggestedBrandId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Brand), request.SuggestedBrandId.Value);

            var selectedBrandHasCategories = selectedBrand.CategoryId.HasValue || selectedBrand.BrandCategories.Count > 0;
            if (selectedBrandHasCategories
                && !await CatalogRequestWorkflowSupport.BrandMatchesCategoryAsync(
                    _context,
                    selectedBrand,
                    request.SuggestedCategoryId.Value,
                    cancellationToken))
            {
                throw new BusinessRuleException("BRAND_CATEGORY_MISMATCH", "The selected brand does not belong to the selected category.");
            }
        }

        if (request.SuggestedUnitOfMeasureId.HasValue
            && !await _context.UnitsOfMeasure.AnyAsync(unit => unit.Id == request.SuggestedUnitOfMeasureId.Value, cancellationToken))
        {
            throw new NotFoundException(nameof(UnitOfMeasure), request.SuggestedUnitOfMeasureId.Value);
        }

        if (request.SuggestedPackageTypeId.HasValue
            && !await _context.UnitsOfMeasure.AnyAsync(unit => unit.Id == request.SuggestedPackageTypeId.Value, cancellationToken))
        {
            throw new NotFoundException(nameof(UnitOfMeasure), request.SuggestedPackageTypeId.Value);
        }

        if (request.SuggestedMeasurementValue.HasValue && request.SuggestedMeasurementValue.Value <= 0)
        {
            throw new BusinessRuleException(
                "INVALID_MEASUREMENT_VALUE",
                "Measurement value must be greater than zero.");
        }

        var normalizedNameAr = CatalogRequestWorkflowSupport.NormalizeName(request.SuggestedNameAr);
        var normalizedNameEn = CatalogRequestWorkflowSupport.NormalizeName(request.SuggestedNameEn);

        BrandRequest? brandRequest = null;
        if (request.RequestedBrand is not null)
        {
            await CatalogRequestWorkflowSupport.EnsureBrandRequestCanBeSubmittedAsync(
                _context,
                vendorId,
                request.RequestedBrand.CategoryId,
                request.RequestedBrand.NameAr,
                request.RequestedBrand.NameEn,
                cancellationToken);

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
            var targetLevelKey = await CatalogRequestWorkflowSupport.ValidateAndResolveCategoryTargetLevelAsync(
                _context,
                vendorId,
                request.RequestedCategory.NameAr,
                request.RequestedCategory.NameEn,
                request.RequestedCategory.TargetLevel,
                request.RequestedCategory.ParentCategoryId,
                cancellationToken);

            categoryRequest = new CategoryRequest(
                vendorId,
                request.RequestedCategory.NameAr,
                request.RequestedCategory.NameEn,
                targetLevelKey,
                request.RequestedCategory.ParentCategoryId,
                request.RequestedCategory.DisplayOrder,
                request.RequestedCategory.ImageUrl);

            _context.CategoryRequests.Add(categoryRequest);
        }

        var duplicatePendingRequestExists = await _context.ProductRequests
            .AsNoTracking()
            .AnyAsync(
                item => item.VendorId == vendorId &&
                        item.Status == Domain.Modules.Catalog.Enums.ApprovalStatus.Pending &&
                        item.SuggestedCategoryId == request.SuggestedCategoryId &&
                        item.SuggestedBrandId == request.SuggestedBrandId &&
                        item.SuggestedUnitOfMeasureId == request.SuggestedUnitOfMeasureId &&
                        item.SuggestedPackageTypeId == request.SuggestedPackageTypeId &&
                        item.SuggestedMeasurementValue == request.SuggestedMeasurementValue &&
                        item.SuggestedNameAr.ToUpper() == normalizedNameAr &&
                        item.SuggestedNameEn.ToUpper() == normalizedNameEn,
                cancellationToken);

        if (duplicatePendingRequestExists)
        {
            throw new BusinessRuleException(
                "PRODUCT_REQUEST_ALREADY_PENDING",
                "A matching product request is already pending review.");
        }

        if (request.SuggestedCategoryId.HasValue)
        {
            var activeProductExists = await _context.MasterProducts
                .AsNoTracking()
                .AnyAsync(
                    item => item.CategoryId == request.SuggestedCategoryId.Value &&
                            item.BrandId == request.SuggestedBrandId &&
                            item.UnitOfMeasureId == request.SuggestedUnitOfMeasureId &&
                            item.PackageTypeId == request.SuggestedPackageTypeId &&
                            item.MeasurementValue == request.SuggestedMeasurementValue &&
                            item.Status == Domain.Modules.Catalog.Enums.ProductStatus.Active &&
                            item.NameAr.ToUpper() == normalizedNameAr &&
                            item.NameEn.ToUpper() == normalizedNameEn,
                    cancellationToken);

            if (activeProductExists)
            {
                throw new BusinessRuleException(
                    "PRODUCT_ALREADY_EXISTS",
                    "A matching catalog product already exists. Add it to your store instead of creating a new request.");
            }
        }

        var imageUrls = ProductRequestImageSupport.ParseImageUrls(
            ProductRequestImageSupport.SerializeImageUrls(request.SuggestedImageUrls),
            request.ImageUrl);
        var primaryImageUrl = imageUrls.FirstOrDefault() ?? request.ImageUrl;

        var productRequest = new ProductRequest(
            vendorId: vendorId,
            suggestedNameAr: request.SuggestedNameAr,
            suggestedNameEn: request.SuggestedNameEn,
            suggestedCategoryId: request.SuggestedCategoryId,
            suggestedCategoryRequestId: categoryRequest?.Id,
            suggestedBrandId: request.SuggestedBrandId,
            suggestedBrandRequestId: brandRequest?.Id,
            suggestedUnitOfMeasureId: request.SuggestedUnitOfMeasureId,
            suggestedPackageTypeId: request.SuggestedPackageTypeId,
            suggestedMeasurementValue: request.SuggestedMeasurementValue,
            suggestedDescriptionAr: request.SuggestedDescriptionAr,
            suggestedDescriptionEn: request.SuggestedDescriptionEn,
            imageUrl: primaryImageUrl,
            suggestedImageUrlsJson: ProductRequestImageSupport.SerializeImageUrls(imageUrls)
        );

        _context.ProductRequests.Add(productRequest);
        await _context.SaveChangesAsync(cancellationToken);

        await _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.CatalogProductRequestSubmitted,
                AdminAlertCategories.Catalog,
                AdminAlertPriorities.Normal,
                "طلب منتج جديد من تاجر",
                "New vendor product request",
                $"أرسلنا طلب منتج جديد: {request.SuggestedNameAr}.",
                $"A new product request was submitted: {request.SuggestedNameEn}.",
                productRequest.Id,
                $"/catalog/products?requests=1&requestId={productRequest.Id}",
                new { productRequestId = productRequest.Id, vendorId }),
            cancellationToken);

        return productRequest.Id;
    }
}
