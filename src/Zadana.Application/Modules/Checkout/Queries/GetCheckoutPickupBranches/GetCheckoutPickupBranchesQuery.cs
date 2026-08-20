using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Checkout.Queries.GetCheckoutPickupBranches;

public record GetCheckoutPickupBranchesQuery(
    Guid UserId,
    Guid VendorId,
    string? City,
    Guid? AddressId) : IRequest<CheckoutPickupBranchesDto>;

public class GetCheckoutPickupBranchesQueryHandler
    : IRequestHandler<GetCheckoutPickupBranchesQuery, CheckoutPickupBranchesDto>
{
    private readonly IApplicationDbContext _context;

    public GetCheckoutPickupBranchesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CheckoutPickupBranchesDto> Handle(
        GetCheckoutPickupBranchesQuery request,
        CancellationToken cancellationToken)
    {
        var city = await ResolveCityAsync(request, cancellationToken);
        var addressCoords = await ResolveAddressCoordinatesAsync(request, cancellationToken);
        return await CheckoutSupport.GetPickupBranchesForCityAsync(
            _context,
            request.UserId,
            request.VendorId,
            city,
            cancellationToken,
            addressCoords?.Latitude,
            addressCoords?.Longitude);
    }

    private async Task<(decimal Latitude, decimal Longitude)?> ResolveAddressCoordinatesAsync(
        GetCheckoutPickupBranchesQuery request,
        CancellationToken cancellationToken)
    {
        if (!request.AddressId.HasValue)
        {
            return null;
        }

        var address = await _context.CustomerAddresses
            .AsNoTracking()
            .Where(item => item.Id == request.AddressId.Value && item.UserId == request.UserId)
            .Select(item => new { item.Latitude, item.Longitude })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("CustomerAddress", request.AddressId.Value);

        if (!address.Latitude.HasValue || !address.Longitude.HasValue
            || (address.Latitude.Value == 0m && address.Longitude.Value == 0m))
        {
            return null;
        }

        return (address.Latitude.Value, address.Longitude.Value);
    }

    private async Task<string> ResolveCityAsync(
        GetCheckoutPickupBranchesQuery request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.City))
        {
            return request.City.Trim();
        }

        if (!request.AddressId.HasValue)
        {
            throw new BusinessRuleException(
                "PICKUP_CITY_REQUIRED",
                "Provide city or address_id to list pickup branches.");
        }

        var address = await _context.CustomerAddresses
            .AsNoTracking()
            .Where(item => item.Id == request.AddressId.Value && item.UserId == request.UserId)
            .Select(item => new { item.City })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("CustomerAddress", request.AddressId.Value);

        if (string.IsNullOrWhiteSpace(address.City))
        {
            throw new BusinessRuleException(
                "PICKUP_CITY_REQUIRED",
                "Selected address does not have a city.");
        }

        return address.City.Trim();
    }
}
