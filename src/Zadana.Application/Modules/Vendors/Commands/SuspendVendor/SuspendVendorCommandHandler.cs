using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.SuspendVendor;

public class SuspendVendorCommandHandler : IRequestHandler<SuspendVendorCommand>
{
    private readonly IApplicationDbContext _db;

    public SuspendVendorCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task Handle(SuspendVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _db.Vendors
            .FirstOrDefaultAsync(v => v.Id == request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.Suspend(request.Reason);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
