using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Queries.GetVendorProfile;

public class GetVendorProfileQueryHandler : IRequestHandler<GetVendorProfileQuery, VendorWorkspaceDto>
{
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentVendorService _currentVendorService;

    public GetVendorProfileQueryHandler(
        IVendorReadService vendorReadService,
        ICurrentUserService currentUser,
        ICurrentVendorService currentVendorService)
    {
        _vendorReadService = vendorReadService;
        _currentUser = currentUser;
        _currentVendorService = currentVendorService;
    }

    public async Task<VendorWorkspaceDto> Handle(GetVendorProfileQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        }

        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var vendor = await _vendorReadService.GetWorkspaceByVendorIdAsync(vendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", vendorId);

        return vendor;
    }
}
