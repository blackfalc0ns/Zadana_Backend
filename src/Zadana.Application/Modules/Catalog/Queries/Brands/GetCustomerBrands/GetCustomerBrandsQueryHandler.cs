using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetCustomerBrands;

public class GetCustomerBrandsQueryHandler : IRequestHandler<GetCustomerBrandsQuery, List<BrandCustomerDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCustomerBrandsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BrandCustomerDto>> Handle(GetCustomerBrandsQuery request, CancellationToken cancellationToken)
    {
        var brands = await _context.Brands
            .AsNoTracking()
            .Where(brand => brand.IsActive)
            .Select(brand => new
            {
                brand.Id,
                brand.NameAr,
                brand.NameEn,
                brand.LogoUrl,
                ProductCount = brand.MasterProducts.Count(product => product.Status == ProductStatus.Active)
            })
            .ToListAsync(cancellationToken);

        return brands
            .Select(brand => new BrandCustomerDto(
                brand.Id,
                BrandCatalogQueryHelpers.PickLocalized(brand.NameAr, brand.NameEn),
                brand.LogoUrl,
                brand.ProductCount))
            .OrderByDescending(brand => brand.ProductCount)
            .ThenBy(brand => brand.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
