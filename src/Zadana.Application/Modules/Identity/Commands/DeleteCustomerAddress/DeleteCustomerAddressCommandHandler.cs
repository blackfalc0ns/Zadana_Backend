using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.DeleteCustomerAddress;

public class DeleteCustomerAddressCommandHandler : IRequestHandler<DeleteCustomerAddressCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteCustomerAddressCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteCustomerAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await _context.CustomerAddresses
            .FirstOrDefaultAsync(x => x.Id == request.AddressId && x.UserId == request.UserId, cancellationToken);

        if (address is null)
        {
            throw new NotFoundException(nameof(CustomerAddress), request.AddressId);
        }

        _context.CustomerAddresses.Remove(address);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
