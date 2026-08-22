using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography.DTOs;
using Zadana.Application.Modules.Geography.Support;

namespace Zadana.Application.Modules.Geography.Queries.GetAdminOperationalRegions;

internal sealed class GetAdminOperationalRegionsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetAdminOperationalRegionsQuery, IReadOnlyList<OperationalRegionDto>>
{
    public async Task<IReadOnlyList<OperationalRegionDto>> Handle(
        GetAdminOperationalRegionsQuery request,
        CancellationToken cancellationToken)
    {
        var regions = await dbContext.SaudiRegions
            .AsNoTracking()
            .OrderBy(region => region.SortOrder)
            .ThenBy(region => region.NameEn)
            .ToListAsync(cancellationToken);

        var cities = await dbContext.SaudiCities
            .AsNoTracking()
            .Include(city => city.Region)
            .OrderBy(city => city.Region.SortOrder)
            .ThenBy(city => city.SortOrder)
            .ThenBy(city => city.NameEn)
            .ToListAsync(cancellationToken);

        var citiesByRegionId = cities
            .GroupBy(city => city.RegionId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return regions
            .Select(region =>
            {
                var regionCities = citiesByRegionId.TryGetValue(region.Id, out var groupedCities)
                    ? groupedCities
                    : [];

                return OperationalGeographyMapper.ToOperationalRegionDto(
                    region,
                    regionCities
                        .Select(city => OperationalGeographyMapper.ToOperationalCityDto(city, region.Code))
                        .ToList());
            })
            .ToList();
    }
}
