using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.AddCustomerAddress;

public class AddCustomerAddressCommandHandler : IRequestHandler<AddCustomerAddressCommand, CustomerAddressDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityAccountService _identityAccountService;

    public AddCustomerAddressCommandHandler(IApplicationDbContext context, IIdentityAccountService identityAccountService)
    {
        _context = context;
        _identityAccountService = identityAccountService;
    }

    public async Task<CustomerAddressDto> Handle(AddCustomerAddressCommand request, CancellationToken cancellationToken)
    {
        var userExists = await _identityAccountService.ExistsByIdAsync(request.UserId, cancellationToken);
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

        var hasExistingDefault = await _context.CustomerAddresses
            .AnyAsync(x => x.UserId == request.UserId && x.IsDefault, cancellationToken);

        if (request.IsDefault || !hasExistingDefault)
        {
            var currentDefaults = await _context.CustomerAddresses
                .Where(x => x.UserId == request.UserId && x.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var currentDefault in currentDefaults)
            {
                currentDefault.RemoveDefault();
            }

            address.SetAsDefault();
        }

        _context.CustomerAddresses.Add(address);
        await _context.SaveChangesAsync(cancellationToken);

        return new CustomerAddressDto(
            address.Id,
            address.ContactName,
            address.ContactPhone,
            address.AddressLine,
            address.Label?.ToString(),
            address.BuildingNo,
            address.FloorNo,
            address.ApartmentNo,
            address.City,
            address.Area,
            address.Latitude,
            address.Longitude,
            address.IsDefault);
    }
}
