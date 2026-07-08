using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Extensions;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Common;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.ReviewRequest;

public class ReviewProductRequestCommandHandler : IRequestHandler<ReviewProductRequestCommand, Guid?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly INotificationService _notificationService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<ReviewProductRequestCommandHandler> _logger;

    public ReviewProductRequestCommandHandler(
        IApplicationDbContext context,
        ICacheInvalidator cacheInvalidator,
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        IStringLocalizer<SharedResource> localizer,
        INotificationService notificationService,
        IFileStorageService fileStorageService,
        ILogger<ReviewProductRequestCommandHandler> logger)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _localizer = localizer;
        _notificationService = notificationService;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<Guid?> Handle(ReviewProductRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.HasRole(UserRole.Admin, UserRole.SuperAdmin))
        {
            throw new ForbiddenAccessException("UNAUTHORIZED_REVIEW_REQUESTS");
        }

        var productRequest = await _context.ProductRequests
            .Include(item => item.Vendor)
            .Include(item => item.BrandRequest)
            .Include(item => item.CategoryRequest)
            .FirstOrDefaultAsync(item => item.Id == request.ProductRequestId, cancellationToken);
        if (productRequest == null)
        {
            throw new NotFoundException(nameof(ProductRequest), request.ProductRequestId);
        }

        if (productRequest.Status != ApprovalStatus.Pending)
        {
            throw new BusinessRuleException("REQUEST_ALREADY_REVIEWED", _localizer["REQUEST_ALREADY_REVIEWED"]);
        }

        var reviewerName = await ResolveReviewerNameAsync(cancellationToken);

        if (request.IsApproved)
        {
            Brand? autoApprovedBrand = null;
            var resolvedCategoryId = productRequest.SuggestedCategoryId;
            if (!resolvedCategoryId.HasValue && productRequest.CategoryRequest is not null)
            {
                if (productRequest.CategoryRequest.Status == ApprovalStatus.Pending)
                {
                    var categoryRequest = productRequest.CategoryRequest;
                    var approvedParentCategoryId = categoryRequest.ParentCategoryId;

                    var normalizedCatNameAr = CatalogRequestWorkflowSupport.NormalizeName(categoryRequest.NameAr);
                    var normalizedCatNameEn = CatalogRequestWorkflowSupport.NormalizeName(categoryRequest.NameEn);

                    var category = await _context.Categories
                        .FirstOrDefaultAsync(
                            item => item.ParentCategoryId == approvedParentCategoryId &&
                                    item.NameAr.ToUpper() == normalizedCatNameAr &&
                                    item.NameEn.ToUpper() == normalizedCatNameEn,
                            cancellationToken);

                    if (category is null)
                    {
                        category = new Category(
                            categoryRequest.NameAr,
                            categoryRequest.NameEn,
                            categoryRequest.ImageUrl,
                            approvedParentCategoryId,
                            categoryRequest.DisplayOrder);

                        _context.Categories.Add(category);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else if (!category.IsActive)
                    {
                        category.Activate();
                    }

                    categoryRequest.Approve(reviewerName, category.Id);

                    await _notificationService.SendToUserAsync(
                        productRequest.Vendor.UserId,
                        "اعتمدنا طلب الكتالوج",
                        "Catalog Request Approved",
                        $"اعتمدنا طلب التصنيف '{categoryRequest.NameAr}' تلقائيًا كجزء من طلب المنتج.",
                        $"Your category request '{categoryRequest.NameEn}' has been approved automatically as part of your product request.",
                        "catalog_request_category",
                        cancellationToken: cancellationToken);
                }

                if (productRequest.CategoryRequest.Status != ApprovalStatus.Approved || !productRequest.CategoryRequest.CreatedCategoryId.HasValue)
                {
                    throw new BusinessRuleException("CATEGORY_REQUEST_NOT_APPROVED", _localizer["CATEGORY_REQUEST_NOT_APPROVED"]);
                }

                resolvedCategoryId = productRequest.CategoryRequest.CreatedCategoryId.Value;
            }

            if (!resolvedCategoryId.HasValue)
            {
                throw new BadRequestException("CATEGORY_REQUIRED", _localizer["RequiredField"]);
            }

            var categoryExists = await _context.Categories
                .AsNoTracking()
                .AnyAsync(item => item.Id == resolvedCategoryId.Value, cancellationToken);
            if (!categoryExists)
            {
                throw new NotFoundException(nameof(Category), resolvedCategoryId.Value);
            }

            var resolvedBrandId = productRequest.SuggestedBrandId;
            if (!resolvedBrandId.HasValue && productRequest.BrandRequest is not null)
            {
                if (productRequest.BrandRequest.Status == ApprovalStatus.Pending)
                {
                    var brandRequest = productRequest.BrandRequest;

                    var brand = await CatalogRequestWorkflowSupport.FindMatchingBrandAsync(
                        _context,
                        brandRequest.NameAr,
                        brandRequest.NameEn,
                        cancellationToken);

                    if (brand is null)
                    {
                        brand = new Brand(brandRequest.NameAr, brandRequest.NameEn, brandRequest.LogoUrl, null, resolvedCategoryId);
                        _context.Brands.Add(brand);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        if (!brand.IsActive)
                        {
                            brand.Activate();
                        }

                        if (string.IsNullOrWhiteSpace(brand.LogoUrl) && !string.IsNullOrWhiteSpace(brandRequest.LogoUrl))
                        {
                            brand.Update(brand.NameAr, brand.NameEn, brandRequest.LogoUrl, brand.CoverImageUrl, brand.CategoryId);
                        }
                    }

                    if (resolvedCategoryId.HasValue && !brand.BrandCategories.Any(link => link.CategoryId == resolvedCategoryId.Value))
                    {
                        var brandCategoryLink = new BrandCategory(brand.Id, resolvedCategoryId.Value);
                        _context.BrandCategories.Add(brandCategoryLink);
                        brand.BrandCategories.Add(brandCategoryLink);
                    }

                    brandRequest.Approve(reviewerName, brand.Id);
                    autoApprovedBrand = brand;

                    await _notificationService.SendToUserAsync(
                        productRequest.Vendor.UserId,
                        "اعتمدنا طلب الكتالوج",
                        "Catalog Request Approved",
                        $"اعتمدنا طلب العلامة التجارية '{brandRequest.NameAr}' تلقائيًا كجزء من طلب المنتج.",
                        $"Your brand request '{brandRequest.NameEn}' has been approved automatically as part of your product request.",
                        "catalog_request_brand",
                        cancellationToken: cancellationToken);
                }

                if (productRequest.BrandRequest.Status != ApprovalStatus.Approved || !productRequest.BrandRequest.CreatedBrandId.HasValue)
                {
                    throw new BusinessRuleException("BRAND_REQUEST_NOT_APPROVED", _localizer["BRAND_REQUEST_NOT_APPROVED"]);
                }

                resolvedBrandId = productRequest.BrandRequest.CreatedBrandId.Value;
            }

            if (resolvedBrandId.HasValue)
            {
                var brand = autoApprovedBrand ?? await _context.Brands
                    .Include(item => item.BrandCategories)
                    .FirstOrDefaultAsync(item => item.Id == resolvedBrandId.Value, cancellationToken)
                    ?? throw new NotFoundException(nameof(Brand), resolvedBrandId.Value);

                if (!CatalogRequestWorkflowSupport.BrandMatchesCategory(brand, resolvedCategoryId.Value))
                {
                    throw new BusinessRuleException("BRAND_CATEGORY_MISMATCH", _localizer["BRAND_CATEGORY_MISMATCH"]);
                }
            }

            var normalizedNameAr = CatalogRequestWorkflowSupport.NormalizeName(productRequest.SuggestedNameAr);
            var normalizedNameEn = CatalogRequestWorkflowSupport.NormalizeName(productRequest.SuggestedNameEn);

            var masterProduct = await _context.MasterProducts
                .Include(item => item.Images)
                .FirstOrDefaultAsync(
                    item => item.CategoryId == resolvedCategoryId.Value &&
                            item.BrandId == resolvedBrandId &&
                            item.UnitOfMeasureId == productRequest.SuggestedUnitOfMeasureId &&
                            item.PackageTypeId == productRequest.SuggestedPackageTypeId &&
                            item.MeasurementValue == productRequest.SuggestedMeasurementValue &&
                            item.NameAr.ToUpper() == normalizedNameAr &&
                            item.NameEn.ToUpper() == normalizedNameEn,
                    cancellationToken);

            if (masterProduct?.Status == ProductStatus.Discontinued)
            {
                throw new BusinessRuleException(
                    "PRODUCT_DISCONTINUED",
                    "A matching discontinued catalog product already exists and cannot be reactivated through request approval.");
            }

            if (masterProduct is null)
            {
                var slug = await CatalogRequestWorkflowSupport.GenerateUniqueMasterProductSlugAsync(
                    _context,
                    productRequest.SuggestedNameEn,
                    cancellationToken);

                masterProduct = new MasterProduct(
                    nameAr: productRequest.SuggestedNameAr,
                    nameEn: productRequest.SuggestedNameEn,
                    slug: slug,
                    categoryId: resolvedCategoryId.Value,
                    brandId: resolvedBrandId,
                    unitOfMeasureId: productRequest.SuggestedUnitOfMeasureId,
                    packageTypeId: productRequest.SuggestedPackageTypeId,
                    measurementValue: productRequest.SuggestedMeasurementValue,
                    measurementUnitId: productRequest.SuggestedUnitOfMeasureId,
                    descriptionAr: productRequest.SuggestedDescriptionAr,
                    descriptionEn: productRequest.SuggestedDescriptionEn
                );

                var imageUrls = ProductRequestImageSupport.ParseImageUrls(
                    productRequest.SuggestedImageUrlsJson,
                    productRequest.ImageUrl);
                for (var index = 0; index < imageUrls.Count; index++)
                {
                    masterProduct.AddImage(imageUrls[index], productRequest.SuggestedNameEn, index, index == 0);
                }

                _context.MasterProducts.Add(masterProduct);
            }

            masterProduct.Publish();
            productRequest.Approve(reviewerName, masterProduct.Id);

            await _notificationService.SendToUserAsync(
                productRequest.Vendor.UserId,
                "اعتمدنا طلب الكتالوج",
                "Catalog Request Approved",
                $"اعتمدنا طلب المنتج '{productRequest.SuggestedNameAr}'.",
                $"Your product request '{productRequest.SuggestedNameEn}' has been approved.",
                "catalog_request_product",
                cancellationToken: cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
            return masterProduct.Id;
        }

        if (string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            throw new BadRequestException("REJECTION_REASON_REQUIRED", _localizer["REJECTION_REASON_REQUIRED"]);
        }

        productRequest.Reject(request.RejectionReason, reviewerName);

        await _notificationService.SendToUserAsync(
            productRequest.Vendor.UserId,
            "رفضنا طلب الكتالوج",
            "Catalog Request Rejected",
            $"رفضنا طلب المنتج '{productRequest.SuggestedNameAr}'. السبب: {request.RejectionReason}",
            $"Your product request '{productRequest.SuggestedNameEn}' was rejected. Reason: {request.RejectionReason}",
            "catalog_request_product",
            cancellationToken: cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var rejectedImageUrls = ProductRequestImageSupport.ParseImageUrls(
            productRequest.SuggestedImageUrlsJson,
            productRequest.ImageUrl);
        foreach (var imageUrl in rejectedImageUrls)
        {
            try
            {
                await _fileStorageService.DeleteAsync(imageUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image file {ImageUrl} for rejected product request {ProductRequestId}",
                    imageUrl, productRequest.Id);
            }
        }

        return null;
    }

    private async Task<string> ResolveReviewerNameAsync(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return "Admin";
        }

        var reviewer = await _identityAccountService.FindByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        return string.IsNullOrWhiteSpace(reviewer?.FullName) ? "Admin" : reviewer.FullName;
    }
}
