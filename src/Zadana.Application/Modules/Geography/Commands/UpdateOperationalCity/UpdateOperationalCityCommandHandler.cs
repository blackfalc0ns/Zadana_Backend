using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography.DTOs;
using Zadana.Application.Modules.Geography.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Geography.Commands.UpdateOperationalCity;

internal sealed class UpdateOperationalCityCommandHandler(
    IApplicationDbContext dbContext,
    IUnitOfWork unitOfWork,
    ICacheInvalidator cacheInvalidator)
    : IRequestHandler<UpdateOperationalCityCommand, OperationalCityDto>
{
    public async Task<OperationalCityDto> Handle(
        UpdateOperationalCityCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedCode = request.CityCode.Trim().ToUpperInvariant();
        var city = await dbContext.SaudiCities
            .Include(item => item.Region)
            .FirstOrDefaultAsync(item => item.Code == normalizedCode, cancellationToken)
            ?? throw new NotFoundException("SaudiCity", normalizedCode);

        city.SetOperational(request.IsOperational);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await GeographyCacheInvalidator.InvalidateRegionsAsync(cacheInvalidator, cancellationToken);
        await GeographyCacheInvalidator.InvalidateCitiesAsync(cacheInvalidator, city.Region.Code, cancellationToken);

        return OperationalGeographyMapper.ToOperationalCityDto(city);
    }
}
