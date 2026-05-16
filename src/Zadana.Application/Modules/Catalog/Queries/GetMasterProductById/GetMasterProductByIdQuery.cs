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

        var variants = await _context.MasterProducts
            .AsNoTracking()
            .Include(p => p.PackageType)
            .Include(p => p.MeasurementUnit)
            .Where(p => p.VariantGroupId == product.VariantGroupId)
            .OrderBy(p => p.MeasurementValue ?? decimal.MaxValue)
            .ThenBy(p => p.NameAr)
            .Select(p => new MasterProductVariantOptionDto(
                p.Id,
                _context.VendorProducts
                    .Where(vp => vp.MasterProductId == p.Id)
                    .OrderBy(vp => vp.CreatedAtUtc)
                    .Select(vp => (Guid?)vp.Id)
                    .FirstOrDefault(),
                p.NameAr,
                p.NameEn,
                MasterProductDisplayDto.BuildDisplaySize(
                    p.PackageType != null ? p.PackageType.NameAr : null,
                    p.MeasurementValue,
                    p.MeasurementUnit != null ? p.MeasurementUnit.NameAr : null,
                    p.MeasurementUnit != null ? p.MeasurementUnit.Symbol : null,
                    true),
                MasterProductDisplayDto.BuildDisplaySize(
                    p.PackageType != null ? p.PackageType.NameEn : null,
                    p.MeasurementValue,
                    p.MeasurementUnit != null ? p.MeasurementUnit.NameEn : null,
                    p.MeasurementUnit != null ? p.MeasurementUnit.Symbol : null,
                    false),
                p.Id == product.Id))
            .ToListAsync(cancellationToken);

        return MasterProductDisplayDto.ToDto(product, false, variants);
    }
}
