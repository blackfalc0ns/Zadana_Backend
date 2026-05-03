using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.DTOs;
using Zadana.Domain.Modules.Finances.Entities;

namespace Zadana.Application.Modules.Finances.Commands.UpdateZoneFinanceSettings;

internal sealed class UpdateZoneFinanceSettingsCommandHandler(IApplicationDbContext dbContext) 
    : IRequestHandler<UpdateZoneFinanceSettingsCommand, ZoneFinanceSettingsDto>
{
    public async Task<ZoneFinanceSettingsDto> Handle(UpdateZoneFinanceSettingsCommand request, CancellationToken cancellationToken)
    {
        var zone = await dbContext.DeliveryZones
            .FirstOrDefaultAsync(x => x.Id == request.ZoneId, cancellationToken);
            
        if (zone == null)
        {
            throw new Exception("Zone not found"); // Should use a proper NotFoundException but this is fine for now
        }

        var settings = await dbContext.ZoneFinanceSettings
            .FirstOrDefaultAsync(x => x.DeliveryZoneId == request.ZoneId, cancellationToken);

        if (settings == null)
        {
            settings = new ZoneFinanceSettings(
                request.ZoneId,
                request.VatPercent,
                request.CodFeeType,
                request.CodFlatFee,
                request.CodPercent,
                request.IsVatActive,
                request.IsCodFeeActive);
                
            await dbContext.ZoneFinanceSettings.AddAsync(settings, cancellationToken);
        }
        else
        {
            settings.Update(
                request.VatPercent,
                request.CodFeeType,
                request.CodFlatFee,
                request.CodPercent,
                request.IsVatActive,
                request.IsCodFeeActive);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var rule = await dbContext.DeliveryPricingRules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DeliveryZoneId == request.ZoneId, cancellationToken);

        return new ZoneFinanceSettingsDto
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
            
            VatPercent = settings.VatPercent,
            CodFeeType = settings.CodFeeType,
            CodFlatFee = settings.CodFlatFee,
            CodPercent = settings.CodPercent,
            IsVatActive = settings.IsVatActive,
            IsCodFeeActive = settings.IsCodFeeActive
        };
    }
}
