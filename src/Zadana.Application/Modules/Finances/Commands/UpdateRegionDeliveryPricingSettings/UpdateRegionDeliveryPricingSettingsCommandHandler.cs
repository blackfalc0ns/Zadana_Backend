using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;
using Zadana.Domain.Modules.Finances.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Finances.Commands.UpdateRegionDeliveryPricingSettings;

internal sealed class UpdateRegionDeliveryPricingSettingsCommandHandler(
    IApplicationDbContext dbContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateRegionDeliveryPricingSettingsCommand, RegionDeliveryPricingSettingsDto>
{
    public async Task<RegionDeliveryPricingSettingsDto> Handle(UpdateRegionDeliveryPricingSettingsCommand request, CancellationToken cancellationToken)
    {
        var region = await dbContext.SaudiRegions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.RegionId, cancellationToken)
            ?? throw new NotFoundException("SaudiRegion", request.RegionId);

        var settings = await dbContext.RegionDeliveryPricingSettings
            .FirstOrDefaultAsync(item => item.SaudiRegionId == request.RegionId, cancellationToken);

        if (settings is null)
        {
            settings = new RegionDeliveryPricingSettings(
                request.RegionId,
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
                request.IsCodFeeActive);

            dbContext.RegionDeliveryPricingSettings.Add(settings);
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
                request.IsCodFeeActive);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegionDeliveryPricingSettingsDto
        {
            RegionId = region.Id,
            RegionCode = region.Code,
            RegionNameAr = region.NameAr,
            RegionNameEn = region.NameEn,
            PricingScope = "region",
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
            IsCodFeeActive = settings.IsCodFeeActive
        };
    }
}
