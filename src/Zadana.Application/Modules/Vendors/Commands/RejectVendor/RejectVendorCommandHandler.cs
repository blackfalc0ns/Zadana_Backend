using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.RejectVendor;

public class RejectVendorCommandHandler : IRequestHandler<RejectVendorCommand>
{
    private readonly IApplicationDbContext _db;

    public RejectVendorCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task Handle(RejectVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _db.Vendors
            .FirstOrDefaultAsync(v => v.Id == request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.Reject(request.Reason);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
