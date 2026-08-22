using Zadana.Application.Modules.Geography.DTOs;
using Zadana.Domain.Modules.Geography.Entities;

namespace Zadana.Application.Modules.Geography.Support;

internal static class OperationalGeographyMapper
{
    public static OperationalRegionDto ToOperationalRegionDto(SaudiRegion region, IReadOnlyList<OperationalCityDto> cities) =>
        new()
        {
            Id = region.Id,
            Code = region.Code,
            NameAr = region.NameAr,
            NameEn = region.NameEn,
            SortOrder = region.SortOrder,
            IsOperational = region.IsOperational,
            Cities = cities
        };

    public static OperationalCityDto ToOperationalCityDto(SaudiCity city, string regionCode) =>
        new()
        {
            Id = city.Id,
            RegionCode = regionCode,
            Code = city.Code,
            NameAr = city.NameAr,
            NameEn = city.NameEn,
            SortOrder = city.SortOrder,
            IsOperational = city.IsOperational
        };

    public static OperationalCityDto ToOperationalCityDto(SaudiCity city) =>
        ToOperationalCityDto(city, city.Region.Code);
}
