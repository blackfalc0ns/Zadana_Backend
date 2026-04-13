using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.UpdateCustomerAddress;

public class UpdateCustomerAddressCommandHandler : IRequestHandler<UpdateCustomerAddressCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateCustomerAddressCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateCustomerAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await _context.CustomerAddresses
            .FirstOrDefaultAsync(x => x.Id == request.AddressId && x.UserId == request.UserId, cancellationToken);

        if (address is null)
        {
            throw new NotFoundException(nameof(CustomerAddress), request.AddressId);
        }

        AddressLabel? parsedLabel = null;
        if (!string.IsNullOrWhiteSpace(request.Label) && Enum.TryParse<AddressLabel>(request.Label, true, out var label))
        {
            parsedLabel = label;
        }

        address.Update(
            request.ContactName,
            request.ContactPhone,
            request.AddressLine,
            parsedLabel,
            request.BuildingNo,
            request.FloorNo,
            request.ApartmentNo,
            request.City,
            request.Area,
            request.Latitude,
            request.Longitude);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
