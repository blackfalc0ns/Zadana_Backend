using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography.DTOs;
using Zadana.Application.Modules.Geography.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Geography.Commands.UpdateOperationalRegion;

internal sealed class UpdateOperationalRegionCommandHandler(
    IApplicationDbContext dbContext,
    IUnitOfWork unitOfWork,
    ICacheInvalidator cacheInvalidator)
    : IRequestHandler<UpdateOperationalRegionCommand, OperationalRegionDto>
{
    public async Task<OperationalRegionDto> Handle(
        UpdateOperationalRegionCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedCode = request.RegionCode.Trim().ToUpperInvariant();
        var region = await dbContext.SaudiRegions
            .FirstOrDefaultAsync(item => item.Code == normalizedCode, cancellationToken)
            ?? throw new NotFoundException("SaudiRegion", normalizedCode);

        region.SetOperational(request.IsOperational);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await GeographyCacheInvalidator.InvalidateRegionsAsync(cacheInvalidator, cancellationToken);

        var cities = await dbContext.SaudiCities
            .AsNoTracking()
            .Where(city => city.RegionId == region.Id)
            .OrderBy(city => city.SortOrder)
            .ThenBy(city => city.NameEn)
            .ToListAsync(cancellationToken);

        return OperationalGeographyMapper.ToOperationalRegionDto(
            region,
            cities.Select(city => OperationalGeographyMapper.ToOperationalCityDto(city, region.Code)).ToList());
    }
}
