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
            .Include(p => p.UnitOfMeasure)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product == null)
            throw new NotFoundException("MasterProduct", request.Id);

        return new MasterProductDto(
            product.Id,
            product.NameAr,
            product.NameEn,
            product.Slug,
            product.DescriptionAr,
            product.DescriptionEn,
            product.Barcode,
            product.CategoryId,
            product.BrandId,
            product.Brand != null ? product.Brand.NameAr : null,
            product.Brand != null ? product.Brand.NameEn : null,
            product.UnitOfMeasureId,
            product.UnitOfMeasure != null ? product.UnitOfMeasure.NameAr : null,
            product.UnitOfMeasure != null ? product.UnitOfMeasure.NameEn : null,
            product.Status.ToString(),
            false,
            product.Images.Select(i => new MasterProductImageDto(i.Url, i.AltText, i.DisplayOrder, i.IsPrimary)).ToList()
        );
    }
}
