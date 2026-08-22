using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Queries.GetRegionDeliveryPricingSettings;

internal sealed class GetRegionDeliveryPricingSettingsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetRegionDeliveryPricingSettingsQuery, List<RegionDeliveryPricingSettingsDto>>
{
    public async Task<List<RegionDeliveryPricingSettingsDto>> Handle(GetRegionDeliveryPricingSettingsQuery request, CancellationToken cancellationToken)
    {
        var regions = await dbContext.SaudiRegions
            .AsNoTracking()
            .Where(item => item.IsOperational)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.NameEn)
            .ToListAsync(cancellationToken);

        var settingsByRegionId = await dbContext.RegionDeliveryPricingSettings
            .AsNoTracking()
            .ToDictionaryAsync(item => item.SaudiRegionId, cancellationToken);

        return regions.Select(region =>
        {
            settingsByRegionId.TryGetValue(region.Id, out var settings);
            return new RegionDeliveryPricingSettingsDto
            {
                RegionId = region.Id,
                RegionCode = region.Code,
                RegionNameAr = region.NameAr,
                RegionNameEn = region.NameEn,
                PricingScope = "region",
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
