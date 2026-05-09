using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Queries.GetZoneFinanceSettings;

internal sealed class GetZoneFinanceSettingsQueryHandler(IApplicationDbContext dbContext) 
    : IRequestHandler<GetZoneFinanceSettingsQuery, List<ZoneFinanceSettingsDto>>
{
    public async Task<List<ZoneFinanceSettingsDto>> Handle(GetZoneFinanceSettingsQuery request, CancellationToken cancellationToken)
    {
        var zones = await dbContext.DeliveryZones
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var pricingRules = await dbContext.DeliveryPricingRules
            .AsNoTracking()
            .Where(x => x.DeliveryZoneId.HasValue)
            .ToDictionaryAsync(x => x.DeliveryZoneId!.Value, cancellationToken);

        var financeSettings = await dbContext.ZoneFinanceSettings
            .AsNoTracking()
            .ToDictionaryAsync(x => x.DeliveryZoneId, cancellationToken);

        var result = new List<ZoneFinanceSettingsDto>();

        foreach (var zone in zones)
        {
            pricingRules.TryGetValue(zone.Id, out var rule);
            financeSettings.TryGetValue(zone.Id, out var settings);

            result.Add(new ZoneFinanceSettingsDto
            {
                ZoneId = zone.Id,
                ZoneName = zone.Name,
                City = zone.City,
                
                BaseDeliveryFee = rule?.BaseFee ?? 0,
                IncludedKm = rule?.IncludedKm ?? 0,
                ExtraKmFee = rule?.PerKmFee ?? 0,
                MinDeliveryFee = rule?.MinFee ?? 0,
                MaxDeliveryFee = rule?.MaxFee ?? 0,
                IsPricingActive = rule?.IsActive ?? false,
                
                VatPercent = settings?.VatPercent ?? 15m, // Default VAT
                CodFeeType = settings?.CodFeeType ?? "flat",
                CodFlatFee = settings?.CodFlatFee ?? 10m,
                CodPercent = settings?.CodPercent ?? 0m,
                IsVatActive = settings?.IsVatActive ?? true,
                IsCodFeeActive = settings?.IsCodFeeActive ?? true
            });
        }

        return result.OrderBy(x => x.City).ThenBy(x => x.ZoneName).ToList();
    }
}
