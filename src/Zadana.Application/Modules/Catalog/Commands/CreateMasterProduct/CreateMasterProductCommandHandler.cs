using FluentValidation;
using MediatR;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Microsoft.EntityFrameworkCore;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;

public class CreateMasterProductCommandHandler : IRequestHandler<CreateMasterProductCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public CreateMasterProductCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<Guid> Handle(CreateMasterProductCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = await _context.Categories.AnyCompatAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        if (request.BrandId.HasValue && !await _context.Brands.AnyCompatAsync(b => b.Id == request.BrandId.Value, cancellationToken))
        {
            throw new NotFoundException("Brand", request.BrandId.Value);
        }

        var measurementUnitId = request.ResolveMeasurementUnitId();

        if (measurementUnitId.HasValue)
        {
            var measurementUnit = await _context.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultCompatAsync(u => u.Id == measurementUnitId.Value, cancellationToken);

            if (measurementUnit is null)
            {
                throw new NotFoundException("UnitOfMeasure", measurementUnitId.Value);
            }

            if (measurementUnit.Kind != UnitKind.Measurement)
            {
                throw new ValidationException("measurementUnitId must refer to a measurement unit.");
            }
        }

        if (request.PackageTypeId.HasValue)
        {
            var packageType = await _context.UnitsOfMeasure
                .AsNoTracking()
                .FirstOrDefaultCompatAsync(u => u.Id == request.PackageTypeId.Value, cancellationToken);

            if (packageType is null)
            {
                throw new NotFoundException("UnitOfMeasure", request.PackageTypeId.Value);
            }

            if (packageType.Kind != UnitKind.Packaging)
            {
                throw new ValidationException("packageTypeId must refer to a packaging unit.");
            }
        }

        if (request.VariantGroupId.HasValue)
        {
            var variantGroupExists = await _context.MasterProducts
                .AsNoTracking()
                .AnyCompatAsync(product => product.VariantGroupId == request.VariantGroupId.Value || product.Id == request.VariantGroupId.Value, cancellationToken);

            if (!variantGroupExists)
            {
                throw new NotFoundException("VariantGroup", request.VariantGroupId.Value);
            }

            // Prevent duplicate variant: same measurement in the same group
            if (request.MeasurementValue.HasValue && request.MeasurementUnitId.HasValue)
            {
                var duplicateExists = await _context.MasterProducts
                    .AsNoTracking()
                    .AnyCompatAsync(product =>
                        product.VariantGroupId == request.VariantGroupId.Value &&
                        product.MeasurementValue == request.MeasurementValue.Value &&
                        product.MeasurementUnitId == request.MeasurementUnitId.Value &&
                        product.PackageTypeId == request.PackageTypeId,
                        cancellationToken);

                if (duplicateExists)
                {
                    throw new BusinessRuleException("DUPLICATE_VARIANT", "A variant with the same size already exists in this group.");
                }
            }
        }

        var masterProduct = new MasterProduct(
            nameAr: request.NameAr,
            nameEn: request.NameEn,
            slug: request.Slug,
            categoryId: request.CategoryId,
            brandId: request.BrandId,
            unitOfMeasureId: measurementUnitId,
            packageTypeId: request.PackageTypeId,
            measurementValue: request.MeasurementValue,
            measurementUnitId: measurementUnitId,
            descriptionAr: request.DescriptionAr,
            descriptionEn: request.DescriptionEn,
            barcode: request.Barcode,
            variantGroupId: request.VariantGroupId
        );

        if (!request.VariantGroupId.HasValue)
        {
            masterProduct.ChangeVariantGroup(masterProduct.Id);
        }

        masterProduct.SetStatus(request.Status);

        if (request.Images != null)
        {
            foreach (var img in request.Images)
            {
                masterProduct.AddImage(img.Url, img.AltText, img.DisplayOrder, img.IsPrimary);
            }
        }

        _context.MasterProducts.Add(masterProduct);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);

        return masterProduct.Id;
    }
}
