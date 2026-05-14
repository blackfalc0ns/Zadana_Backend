using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;
using Zadana.Domain.Modules.Finances.Entities;

namespace Zadana.Application.Modules.Finances.Commands.UpdateDeliveryPricingDefaults;

internal sealed class UpdateDeliveryPricingDefaultsCommandHandler(
    IApplicationDbContext dbContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateDeliveryPricingDefaultsCommand, DeliveryPricingDefaultsDto>
{
    public async Task<DeliveryPricingDefaultsDto> Handle(UpdateDeliveryPricingDefaultsCommand request, CancellationToken cancellationToken)
    {
        var settings = await dbContext.DeliveryPricingDefaults.FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new DeliveryPricingDefaults(
                request.Id,
                request.BaseDeliveryFee,
                request.IncludedKm,
                request.ExtraKmFee,
                request.MinDeliveryFee,
                request.MaxDeliveryFee,
                request.IsPricingActive,
                request.VatPercent,
                request.CodFeeType,
                request.CodFlatFee,
                request.CodPercent,
                request.IsVatActive,
                request.IsCodFeeActive,
                request.MinTotalDeliveryFee,
                request.MaxTotalDeliveryFee,
                request.MaxQuotedDistanceKm,
                request.WarningSubtotalRatioThreshold);

            dbContext.DeliveryPricingDefaults.Add(settings);
        }
        else
        {
            settings.Update(
                request.BaseDeliveryFee,
                request.IncludedKm,
                request.ExtraKmFee,
                request.MinDeliveryFee,
                request.MaxDeliveryFee,
                request.IsPricingActive,
                request.VatPercent,
                request.CodFeeType,
                request.CodFlatFee,
                request.CodPercent,
                request.IsVatActive,
                request.IsCodFeeActive,
                request.MinTotalDeliveryFee,
                request.MaxTotalDeliveryFee,
                request.MaxQuotedDistanceKm,
                request.WarningSubtotalRatioThreshold);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeliveryPricingDefaultsDto
        {
            Id = settings.Id,
            PricingScope = "global",
            BaseDeliveryFee = settings.BaseDeliveryFee,
            IncludedKm = settings.IncludedKm,
            ExtraKmFee = settings.ExtraKmFee,
            MinDeliveryFee = settings.MinDeliveryFee,
            MaxDeliveryFee = settings.MaxDeliveryFee,
            IsPricingActive = settings.IsPricingActive,
            VatPercent = settings.VatPercent,
            CodFeeType = settings.CodFeeType,
            CodFlatFee = settings.CodFlatFee,
            CodPercent = settings.CodPercent,
            IsVatActive = settings.IsVatActive,
            IsCodFeeActive = settings.IsCodFeeActive,
            MinTotalDeliveryFee = settings.MinTotalDeliveryFee,
            MaxTotalDeliveryFee = settings.MaxTotalDeliveryFee,
            MaxQuotedDistanceKm = settings.MaxQuotedDistanceKm,
            WarningSubtotalRatioThreshold = settings.WarningSubtotalRatioThreshold
        };
    }
}
