using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.AddCustomerAddress;

public class AddCustomerAddressCommandHandler : IRequestHandler<AddCustomerAddressCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public AddCustomerAddressCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(AddCustomerAddressCommand request, CancellationToken cancellationToken)
    {
        // Check if user exists
        var userExists = _context.Users.Any(u => u.Id == request.UserId);
        if (!userExists)
            throw new NotFoundException("User", request.UserId);

        AddressLabel? parsedLabel = null;
        if (!string.IsNullOrWhiteSpace(request.Label) && Enum.TryParse<AddressLabel>(request.Label, true, out var l))
        {
            parsedLabel = l;
        }

        var address = new CustomerAddress(
            userId: request.UserId,
            contactName: request.ContactName,
            contactPhone: request.ContactPhone,
            addressLine: request.AddressLine,
            label: parsedLabel,
            buildingNo: request.BuildingNo,
            floorNo: request.FloorNo,
            apartmentNo: request.ApartmentNo,
            city: request.City,
            area: request.Area,
            latitude: request.Latitude,
            longitude: request.Longitude
        );

        _context.CustomerAddresses.Add(address);
        await _context.SaveChangesAsync(cancellationToken);

        return address.Id;
    }
}
