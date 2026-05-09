using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetCustomerBrands;

public class GetCustomerBrandsQueryHandler : IRequestHandler<GetCustomerBrandsQuery, List<BrandCustomerDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAppCache _cache;
    private readonly CacheDurationSettings _durations;

    public GetCustomerBrandsQueryHandler(
        IApplicationDbContext context,
        IAppCache cache,
        IOptions<CachingSettings> cachingOptions)
    {
        _context = context;
        _cache = cache;
        _durations = cachingOptions.Value.Durations;
    }

    public async Task<List<BrandCustomerDto>> Handle(GetCustomerBrandsQuery request, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
            CatalogQueryCacheKeys.CustomerBrands(),
            async token =>
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
                        brand.CoverImageUrl,
                        ProductCount = brand.MasterProducts.Count(product => product.Status == ProductStatus.Active)
                    })
                    .ToListAsync(token);

                return brands
                    .Select(brand => new BrandCustomerDto(
                        brand.Id,
                        BrandCatalogQueryHelpers.PickLocalized(brand.NameAr, brand.NameEn),
                        brand.LogoUrl,
                        brand.CoverImageUrl,
                        brand.ProductCount))
                    .OrderByDescending(brand => brand.ProductCount)
                    .ThenBy(brand => brand.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            },
            new AppCacheEntryOptions(_durations.PublicCatalogMetadata),
            [CacheTagNames.Catalog, CacheTagNames.CatalogFilters],
            cancellationToken);
    }
}
