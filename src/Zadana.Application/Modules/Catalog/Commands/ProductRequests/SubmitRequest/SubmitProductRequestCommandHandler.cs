using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
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

        if (request.SuggestedUnitOfMeasureId.HasValue
            && !await _context.UnitsOfMeasure.AnyAsync(unit => unit.Id == request.SuggestedUnitOfMeasureId.Value, cancellationToken))
        {
            throw new NotFoundException(nameof(UnitOfMeasure), request.SuggestedUnitOfMeasureId.Value);
        }

        BrandRequest? brandRequest = null;
        if (request.RequestedBrand is not null)
        {
            brandRequest = new BrandRequest(
                vendorId,
                request.RequestedBrand.NameAr,
                request.RequestedBrand.NameEn,
                request.RequestedBrand.LogoUrl);

            _context.BrandRequests.Add(brandRequest);
        }

        CategoryRequest? categoryRequest = null;
        if (request.RequestedCategory is not null)
        {
            categoryRequest = new CategoryRequest(
                vendorId,
                request.RequestedCategory.NameAr,
                request.RequestedCategory.NameEn,
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
}
