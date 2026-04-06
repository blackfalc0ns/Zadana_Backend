using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Queries.GetVendorProfile;

public class GetVendorProfileQueryHandler : IRequestHandler<GetVendorProfileQuery, VendorProfileDto>
{
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUser;

    public GetVendorProfileQueryHandler(IVendorReadService vendorReadService, ICurrentUserService currentUser)
    {
        _vendorReadService = vendorReadService;
        _currentUser = currentUser;
    }

    public async Task<VendorProfileDto> Handle(GetVendorProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        var vendor = await _vendorReadService.GetProfileByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        return vendor;
    }
}
