using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.UpdateMasterProduct;

public class UpdateMasterProductCommandHandler : IRequestHandler<UpdateMasterProductCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateMasterProductCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<Unit> Handle(UpdateMasterProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.MasterProducts
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product == null)
            throw new NotFoundException("MasterProduct", request.Id);

        var categoryExists = await _context.Categories.AnyCompatAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
            throw new NotFoundException("Category", request.CategoryId);

        if (request.BrandId.HasValue && !await _context.Brands.AnyCompatAsync(b => b.Id == request.BrandId.Value, cancellationToken))
            throw new NotFoundException("Brand", request.BrandId.Value);

        var measurementUnitId = request.ResolveMeasurementUnitId();
        if (measurementUnitId.HasValue)
        {
            var measurementUnit = await _context.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultCompatAsync(u => u.Id == measurementUnitId.Value, cancellationToken);

            if (measurementUnit is null)
                throw new NotFoundException("UnitOfMeasure", measurementUnitId.Value);

            if (measurementUnit.Kind != UnitKind.Measurement)
                throw new ValidationException("measurementUnitId must refer to a measurement unit.");
        }

        if (request.PackageTypeId.HasValue)
        {
            var packageType = await _context.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultCompatAsync(u => u.Id == request.PackageTypeId.Value, cancellationToken);

            if (packageType is null)
                throw new NotFoundException("UnitOfMeasure", request.PackageTypeId.Value);

            if (packageType.Kind != UnitKind.Packaging)
                throw new ValidationException("packageTypeId must refer to a packaging unit.");
        }

        if (request.VariantGroupId.HasValue)
        {
            var variantGroupExists = await _context.MasterProducts
                .AsNoTracking()
                .AnyCompatAsync(p => p.Id == request.VariantGroupId.Value || p.VariantGroupId == request.VariantGroupId.Value, cancellationToken);

            if (!variantGroupExists)
                throw new NotFoundException("VariantGroup", request.VariantGroupId.Value);
        }

        product.UpdateDetails(
            nameAr: request.NameAr,
            nameEn: request.NameEn,
            slug: request.Slug,
            descriptionAr: request.DescriptionAr,
            descriptionEn: request.DescriptionEn,
            barcode: request.Barcode
        );

        product.ChangeCategory(request.CategoryId);
        product.ChangeBrand(request.BrandId);
        product.ChangeMeasurement(request.MeasurementValue, measurementUnitId);
        product.ChangePackageType(request.PackageTypeId);

        if (request.VariantGroupId.HasValue)
        {
            product.ChangeVariantGroup(request.VariantGroupId.Value);
        }
        else if (product.VariantGroupId == Guid.Empty)
        {
            product.ChangeVariantGroup(product.Id);
        }

        if (request.Status.HasValue)
        {
            product.SetStatus(request.Status.Value);
        }

        // Update Images
        if (request.Images != null)
        {
            product.ClearImages();
            foreach (var img in request.Images)
            {
                product.AddImage(img.Url, img.AltText, img.DisplayOrder, img.IsPrimary);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);

        return Unit.Value;
    }
}
