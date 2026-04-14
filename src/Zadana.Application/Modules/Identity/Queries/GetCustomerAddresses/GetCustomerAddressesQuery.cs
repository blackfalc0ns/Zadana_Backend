using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Queries.GetCustomerAddresses;

public record GetCustomerAddressesQuery(Guid UserId) : IRequest<IReadOnlyList<CustomerAddressDto>>;

public class GetCustomerAddressesQueryHandler : IRequestHandler<GetCustomerAddressesQuery, IReadOnlyList<CustomerAddressDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCustomerAddressesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CustomerAddressDto>> Handle(GetCustomerAddressesQuery request, CancellationToken cancellationToken)
    {
        return await _context.CustomerAddresses
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.City)
            .ThenBy(x => x.Area)
            .ThenBy(x => x.AddressLine)
            .Select(x => new CustomerAddressDto(
                x.Id,
                x.ContactName,
                x.ContactPhone,
                x.AddressLine,
                x.Label.HasValue ? x.Label.Value.ToString() : null,
                x.BuildingNo,
                x.FloorNo,
                x.ApartmentNo,
                x.City,
                x.Area,
                x.Latitude,
                x.Longitude,
                x.IsDefault))
            .ToListAsync(cancellationToken);
    }
}
