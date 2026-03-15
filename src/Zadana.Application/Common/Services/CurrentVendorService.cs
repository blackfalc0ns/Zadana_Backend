using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Common.Services;

public class CurrentVendorService : ICurrentVendorService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CurrentVendorService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Guid?> TryGetVendorIdAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId is null || !_currentUserService.IsAuthenticated)
        {
            return null;
        }

        if (!string.Equals(_currentUserService.Role, UserRole.Vendor.ToString(), StringComparison.Ordinal))
        {
            return null;
        }

        return await _context.Vendors
            .AsNoTracking()
            .Where(v => v.UserId == userId.Value)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> GetRequiredVendorIdAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        }

        if (!string.Equals(_currentUserService.Role, UserRole.Vendor.ToString(), StringComparison.Ordinal))
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
