using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;

namespace Zadana.Application.Modules.Finances.Queries.GetDeliveryPricingDefaults;

internal sealed class GetDeliveryPricingDefaultsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetDeliveryPricingDefaultsQuery, DeliveryPricingDefaultsDto>
{
    private static readonly Guid DefaultId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public async Task<DeliveryPricingDefaultsDto> Handle(GetDeliveryPricingDefaultsQuery request, CancellationToken cancellationToken)
    {
        var settings = await dbContext.DeliveryPricingDefaults
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return new DeliveryPricingDefaultsDto
        {
            Id = settings?.Id ?? DefaultId,
            PricingScope = "global",
            BaseDeliveryFee = settings?.BaseDeliveryFee ?? 15m,
            IncludedKm = settings?.IncludedKm ?? 5m,
            ExtraKmFee = settings?.ExtraKmFee ?? 2m,
            MinDeliveryFee = settings?.MinDeliveryFee ?? 15m,
            MaxDeliveryFee = settings?.MaxDeliveryFee ?? 120m,
            IsPricingActive = settings?.IsPricingActive ?? true,
            VatPercent = settings?.VatPercent ?? 15m,
            CodFeeType = settings?.CodFeeType ?? "flat",
            CodFlatFee = settings?.CodFlatFee ?? 10m,
            CodPercent = settings?.CodPercent ?? 0m,
            IsVatActive = settings?.IsVatActive ?? true,
            IsCodFeeActive = settings?.IsCodFeeActive ?? true,
            MinTotalDeliveryFee = settings?.MinTotalDeliveryFee ?? 15m,
            MaxTotalDeliveryFee = settings?.MaxTotalDeliveryFee ?? 200m,
            MaxQuotedDistanceKm = settings?.MaxQuotedDistanceKm ?? 100m,
            WarningSubtotalRatioThreshold = settings?.WarningSubtotalRatioThreshold ?? 0.75m
        };
    }
}
