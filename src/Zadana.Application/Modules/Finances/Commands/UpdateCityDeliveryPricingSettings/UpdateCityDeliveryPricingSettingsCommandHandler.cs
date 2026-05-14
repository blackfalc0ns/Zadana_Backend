using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;
using Zadana.Domain.Modules.Finances.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Finances.Commands.UpdateCityDeliveryPricingSettings;

internal sealed class UpdateCityDeliveryPricingSettingsCommandHandler(
    IApplicationDbContext dbContext,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateCityDeliveryPricingSettingsCommand, CityDeliveryPricingSettingsDto>
{
    public async Task<CityDeliveryPricingSettingsDto> Handle(UpdateCityDeliveryPricingSettingsCommand request, CancellationToken cancellationToken)
    {
        var city = await dbContext.SaudiCities
            .Include(item => item.Region)
            .FirstOrDefaultAsync(item => item.Id == request.CityId, cancellationToken)
            ?? throw new NotFoundException("SaudiCity", request.CityId);

        var settings = await dbContext.CityDeliveryPricingSettings
            .FirstOrDefaultAsync(item => item.SaudiCityId == request.CityId, cancellationToken);

        if (settings is null)
        {
            settings = new CityDeliveryPricingSettings(
                request.CityId,
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

            await dbContext.CityDeliveryPricingSettings.AddAsync(settings, cancellationToken);
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
