using Zadana.Application.Common.Extensions;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Common.Services;

public class CurrentVendorService : ICurrentVendorService
{
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUserService;

    public CurrentVendorService(IVendorReadService vendorReadService, ICurrentUserService currentUserService)
    {
        _vendorReadService = vendorReadService;
        _currentUserService = currentUserService;
    }

    public async Task<Guid?> TryGetVendorIdAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId is null || !_currentUserService.IsAuthenticated)
        {
            return null;
        }

        if (!_currentUserService.HasRole(UserRole.Vendor))
        {
            return null;
        }

        return await _vendorReadService.GetVendorIdByUserIdAsync(userId.Value, cancellationToken);
    }

    public async Task<Guid> GetRequiredVendorIdAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        }

        if (!_currentUserService.HasRole(UserRole.Vendor))
        {
            throw new UnauthorizedException("VENDORS_ONLY");
        }

        var vendorId = await TryGetVendorIdAsync(cancellationToken);
        if (vendorId is null)
        {
            throw new NotFoundException("Vendor", _currentUserService.UserId.Value);
        }

        return vendorId.Value;
    }
}
