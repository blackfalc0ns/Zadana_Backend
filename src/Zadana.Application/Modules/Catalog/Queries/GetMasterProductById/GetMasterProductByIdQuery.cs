using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.GetMasterProductById;

public record GetMasterProductByIdQuery(Guid Id) : IRequest<MasterProductDto>;

public class GetMasterProductByIdQueryHandler : IRequestHandler<GetMasterProductByIdQuery, MasterProductDto>
{
    private readonly IApplicationDbContext _context;

    public GetMasterProductByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MasterProductDto> Handle(GetMasterProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _context.MasterProducts
            .Include(p => p.Images)
            .Include(p => p.Brand)
            .Include(p => p.PackageType)
            .Include(p => p.MeasurementUnit)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product == null)
            throw new NotFoundException("MasterProduct", request.Id);

        var variantGroupKey = product.VariantGroupId != Guid.Empty
            ? product.VariantGroupId
            : product.Id;

        // Legacy rows may still have VariantGroupId = Guid.Empty. Treating that as
        // "match all empty groups" used to scan the entire catalog and made edit
        // pages time out. Mirror the grouping logic used elsewhere in catalog queries.
        var variantRows = await _context.MasterProducts
            .AsNoTracking()
            .Include(p => p.PackageType)
            .Include(p => p.MeasurementUnit)
            .Include(p => p.Images)
            .Where(p =>
                (p.VariantGroupId != Guid.Empty && p.VariantGroupId == variantGroupKey) ||
                (p.VariantGroupId == Guid.Empty && p.Id == variantGroupKey))
            .OrderBy(p => p.MeasurementValue ?? decimal.MaxValue)
            .ThenBy(p => p.NameAr)
            .ToListAsync(cancellationToken);

        var variantIds = variantRows.Select(p => p.Id).ToList();
        var defaultVendorProductByMasterId = variantIds.Count == 0
            ? new Dictionary<Guid, Guid?>()
            : await _context.VendorProducts
                .AsNoTracking()
                .Where(vp => variantIds.Contains(vp.MasterProductId))
                .GroupBy(vp => vp.MasterProductId)
                .Select(group => new
                {
                    MasterProductId = group.Key,
                    VendorProductId = group
                        .OrderBy(vp => vp.CreatedAtUtc)
                        .Select(vp => (Guid?)vp.Id)
                        .FirstOrDefault()
                })
                .ToDictionaryAsync(item => item.MasterProductId, item => item.VendorProductId, cancellationToken);

        var variants = variantRows
            .Select(p => new MasterProductVariantOptionDto(
                p.Id,
                defaultVendorProductByMasterId.GetValueOrDefault(p.Id),
                p.NameAr,
                p.NameEn,
                MasterProductDisplayDto.BuildDisplaySize(
                    p.PackageType?.NameAr,
                    p.MeasurementValue,
                    p.MeasurementUnit?.NameAr,
                    p.MeasurementUnit?.Symbol,
                    true),
                MasterProductDisplayDto.BuildDisplaySize(
                    p.PackageType?.NameEn,
                    p.MeasurementValue,
                    p.MeasurementUnit?.NameEn,
                    p.MeasurementUnit?.Symbol,
                    false),
                p.Id == product.Id,
                p.Images
                    .OrderByDescending(img => img.IsPrimary)
                    .ThenBy(img => img.DisplayOrder)
                    .Select(img => img.Url)
                    .FirstOrDefault(),
                p.Barcode,
                p.Slug,
                p.PackageTypeId,
                p.MeasurementValue,
                p.MeasurementUnitId))
            .ToList();

        return MasterProductDisplayDto.ToDto(product, false, variants);
    }
}
