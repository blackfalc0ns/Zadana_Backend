using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;
using Zadana.Application.Modules.Geography.Support;

namespace Zadana.Application.Modules.Finances.Queries.GetCityDeliveryPricingSettings;

internal sealed class GetCityDeliveryPricingSettingsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetCityDeliveryPricingSettingsQuery, List<CityDeliveryPricingSettingsDto>>
{
    public async Task<List<CityDeliveryPricingSettingsDto>> Handle(GetCityDeliveryPricingSettingsQuery request, CancellationToken cancellationToken)
    {
        var cities = await dbContext.SaudiCities
            .AsNoTracking()
            .Include(city => city.Region)
            .Where(city => city.Region.Code == OperationalGeographyScope.EasternRegionCode)
            .OrderBy(city => city.Region.SortOrder)
            .ThenBy(city => city.SortOrder)
            .ToListAsync(cancellationToken);

        var settingsByCityId = await dbContext.CityDeliveryPricingSettings
            .AsNoTracking()
            .ToDictionaryAsync(item => item.SaudiCityId, cancellationToken);

        return cities.Select(city =>
        {
            settingsByCityId.TryGetValue(city.Id, out var settings);

            return new CityDeliveryPricingSettingsDto
            {
                CityId = city.Id,
                CityCode = city.Code,
                CityNameAr = city.NameAr,
                CityNameEn = city.NameEn,
                RegionId = city.RegionId,
                RegionCode = city.Region.Code,
                RegionNameAr = city.Region.NameAr,
                RegionNameEn = city.Region.NameEn,
                PricingScope = "city",
                BaseDeliveryFee = settings?.BaseDeliveryFee ?? 0m,
                IncludedKm = settings?.IncludedKm ?? 5m,
                ExtraKmFee = settings?.ExtraKmFee ?? 0m,
                MinDeliveryFee = settings?.MinDeliveryFee ?? 0m,
                MaxDeliveryFee = settings?.MaxDeliveryFee ?? 0m,
                IsPricingActive = settings?.IsPricingActive ?? false,
                VatPercent = settings?.VatPercent ?? 15m,
                CodFeeType = settings?.CodFeeType ?? "flat",
                CodFlatFee = settings?.CodFlatFee ?? 0m,
                CodPercent = settings?.CodPercent ?? 0m,
                IsVatActive = settings?.IsVatActive ?? true,
                IsCodFeeActive = settings?.IsCodFeeActive ?? false
            };
        }).ToList();
    }
}
