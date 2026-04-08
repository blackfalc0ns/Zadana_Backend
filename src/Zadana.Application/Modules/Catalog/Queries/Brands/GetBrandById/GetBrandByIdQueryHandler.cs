using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandById;

public class GetBrandByIdQueryHandler : IRequestHandler<GetBrandByIdQuery, BrandCustomerDto>
{
    private readonly IApplicationDbContext _context;

    public GetBrandByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BrandCustomerDto> Handle(GetBrandByIdQuery request, CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .AsNoTracking()
            .Where(item => item.Id == request.BrandId && item.IsActive)
            .Select(item => new
            {
                item.Id,
                item.NameAr,
                item.NameEn,
                item.LogoUrl,
                ProductCount = item.MasterProducts.Count(product => product.Status == ProductStatus.Active)
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Brand), request.BrandId);

        return new BrandCustomerDto(
            brand.Id,
            BrandCatalogQueryHelpers.PickLocalized(brand.NameAr, brand.NameEn),
            brand.LogoUrl,
            brand.ProductCount);
    }
}
