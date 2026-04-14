using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.SetDefaultCustomerAddress;

public class SetDefaultCustomerAddressCommandHandler : IRequestHandler<SetDefaultCustomerAddressCommand>
{
    private readonly IApplicationDbContext _context;

    public SetDefaultCustomerAddressCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(SetDefaultCustomerAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await _context.CustomerAddresses
            .FirstOrDefaultAsync(x => x.Id == request.AddressId && x.UserId == request.UserId, cancellationToken);

        if (address is null)
        {
            throw new NotFoundException(nameof(CustomerAddress), request.AddressId);
        }

        var currentDefaults = await _context.CustomerAddresses
            .Where(x => x.UserId == request.UserId && x.IsDefault && x.Id != request.AddressId)
            .ToListAsync(cancellationToken);

        foreach (var currentDefault in currentDefaults)
        {
            currentDefault.RemoveDefault();
        }

        address.SetAsDefault();
        await _context.SaveChangesAsync(cancellationToken);
    }
}
