using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.ApproveVendor;

public class ApproveVendorCommandHandler : IRequestHandler<ApproveVendorCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public ApproveVendorCommandHandler(IApplicationDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task Handle(ApproveVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _db.Vendors
            .FirstOrDefaultAsync(v => v.Id == request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        var adminId = _currentUserService.UserId
            ?? throw new UnauthorizedException("لم يتم التعرف على المستخدم. | User could not be identified.");

        vendor.Approve(request.CommissionRate, adminId);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
