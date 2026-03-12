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
            product.UnitOfMeasureId,
            product.Status.ToString(),
            product.Images.Select(i => new MasterProductImageDto(i.Url, i.AltText, i.DisplayOrder, i.IsPrimary)).ToList()
        );
    }
}
