using Zadana.Application.Common.Extensions;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Common.Services;

public class CurrentVendorService : ICurrentVendorService
{
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;

    public CurrentVendorService(
        IVendorReadService vendorReadService,
        ICurrentUserService currentUserService,
        IApplicationDbContext context)
    {
        _vendorReadService = vendorReadService;
        _currentUserService = currentUserService;
        _context = context;
    }

    public async Task<Guid?> TryGetVendorIdAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId is null || !_currentUserService.IsAuthenticated)
        {
            return null;
        }

        if (_currentUserService.HasRole(UserRole.Vendor))
        {
            return await _vendorReadService.GetVendorIdByUserIdAsync(userId.Value, cancellationToken);
        }

        if (!_currentUserService.HasRole(UserRole.VendorStaff))
        {
            return null;
        }

        var scope = await _context.UserAccessScopes
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId.Value &&
                item.IsActive &&
                item.PanelScope == PanelScope.VendorPanel &&
                item.ScopeEntityId.HasValue)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new { item.ScopeType, item.ScopeEntityId })
            .FirstOrDefaultAsync(cancellationToken);

        if (scope is null)
        {
            return null;
        }

        var scopeEntityId = scope.ScopeEntityId.GetValueOrDefault();
        if (scopeEntityId == Guid.Empty)
        {
            return null;
        }

        if (scope.ScopeType == AccessScopeType.VendorCompany)
        {
            return scopeEntityId;
        }

        if (scope.ScopeType == AccessScopeType.VendorBranch)
        {
            return await _context.VendorBranches
                .AsNoTracking()
                .Where(branch => branch.Id == scopeEntityId)
                .Select(branch => (Guid?)branch.VendorId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    public async Task<Guid> GetRequiredVendorIdAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        }

        if (!_currentUserService.HasRole(UserRole.Vendor) &&
            !_currentUserService.HasRole(UserRole.VendorStaff))
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
