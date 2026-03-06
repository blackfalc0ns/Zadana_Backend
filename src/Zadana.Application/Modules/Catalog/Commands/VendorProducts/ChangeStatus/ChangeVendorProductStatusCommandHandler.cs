using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.VendorProducts.ChangeStatus;

public class ChangeVendorProductStatusCommandHandler : IRequestHandler<ChangeVendorProductStatusCommand>
{
    private readonly IApplicationDbContext _context;

    public ChangeVendorProductStatusCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(ChangeVendorProductStatusCommand request, CancellationToken cancellationToken)
    {
        var vendorProduct = await _context.VendorProducts
            .FirstOrDefaultAsync(vp => vp.Id == request.Id && vp.VendorId == request.VendorId, cancellationToken);

        if (vendorProduct == null)
            throw new NotFoundException(nameof(VendorProduct), request.Id);

        if (request.IsActive)
        {
            if (vendorProduct.Status == VendorProductStatus.Suspended)
            {
                 vendorProduct.Activate();
            }
        }
        else
        {
            if (vendorProduct.Status == VendorProductStatus.Active || vendorProduct.Status == VendorProductStatus.OutOfStock)
            {
                 vendorProduct.Suspend();
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
