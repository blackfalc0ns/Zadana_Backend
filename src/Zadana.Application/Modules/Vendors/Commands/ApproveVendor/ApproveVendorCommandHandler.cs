using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.ApproveVendor;

public class ApproveVendorCommandHandler : IRequestHandler<ApproveVendorCommand>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ApproveVendorCommandHandler(
        IVendorRepository vendorRepository,
        IIdentityAccountService identityAccountService,
        IVendorReviewAuditService vendorReviewAuditService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _identityAccountService = identityAccountService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task Handle(ApproveVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        var adminId = _currentUserService.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        vendor.Approve(request.CommissionRate, adminId);

        var activateResult = await _identityAccountService.ActivateAsync(vendor.UserId, cancellationToken);
        if (!activateResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_ACTIVATE_FAILED", string.Join(", ", activateResult.Errors ?? []));
        }

        var unlockResult = await _identityAccountService.UnlockLoginAsync(vendor.UserId, cancellationToken);
        if (!unlockResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_UNLOCK_FAILED", string.Join(", ", unlockResult.Errors ?? []));
        }

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "approved",
            "success",
            $"Vendor approved with commission rate {request.CommissionRate:0.##}%.",
            "Compliance Review",
            "Admin",
            adminId,
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
