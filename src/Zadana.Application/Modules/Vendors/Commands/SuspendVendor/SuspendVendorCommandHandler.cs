using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.SuspendVendor;

public class SuspendVendorCommandHandler : IRequestHandler<SuspendVendorCommand>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IUnitOfWork _unitOfWork;

    public SuspendVendorCommandHandler(
        IVendorRepository vendorRepository,
        IIdentityAccountService identityAccountService,
        IRefreshTokenStore refreshTokenStore,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _identityAccountService = identityAccountService;
        _refreshTokenStore = refreshTokenStore;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(SuspendVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.Suspend(request.Reason);

        var suspendResult = await _identityAccountService.SuspendAsync(vendor.UserId, cancellationToken);
        if (!suspendResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_SUSPEND_FAILED", string.Join(", ", suspendResult.Errors ?? []));
        }

        await _refreshTokenStore.RevokeAllByUserAsync(vendor.UserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
