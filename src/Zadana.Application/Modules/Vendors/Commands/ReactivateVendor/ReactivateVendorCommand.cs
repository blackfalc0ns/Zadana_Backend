using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.ReactivateVendor;

public record ReactivateVendorCommand(Guid VendorId) : IRequest;

public class ReactivateVendorCommandValidator : AbstractValidator<ReactivateVendorCommand>
{
    public ReactivateVendorCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty();
    }
}

public class ReactivateVendorCommandHandler : IRequestHandler<ReactivateVendorCommand>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ReactivateVendorCommandHandler(
        IVendorRepository vendorRepository,
        IIdentityAccountService identityAccountService,
        IVendorReviewAuditService vendorReviewAuditService,
        IVendorCommunicationService vendorCommunicationService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _identityAccountService = identityAccountService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _vendorCommunicationService = vendorCommunicationService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task Handle(ReactivateVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        if (vendor.ArchivedAtUtc.HasValue)
        {
            throw new BusinessRuleException("VENDOR_ARCHIVED", "Archived vendors cannot be reactivated.");
        }

        if (vendor.LockedAtUtc.HasValue && string.IsNullOrWhiteSpace(vendor.SuspensionReason))
        {
            throw new BusinessRuleException("VENDOR_LOGIN_LOCKED", "Unlock vendor login instead of reactivating the account.");
        }

        var actorUserId = _currentUserService.UserId ?? vendor.ApprovedBy ?? vendor.UserId;
        vendor.Reactivate(actorUserId);

        var activateResult = await _identityAccountService.ActivateAsync(vendor.UserId, cancellationToken);
        if (!activateResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_ACTIVATE_FAILED", string.Join(", ", activateResult.Errors ?? []));
        }

        await _vendorReviewAuditService.AppendEntryAsync(
            vendor.UserId,
            "reactivated",
            "success",
            "Vendor account reactivated and returned to active status.",
            "Risk & Compliance",
            "Risk & Compliance Desk",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_reactivated",
                "تم إعادة تشغيل حساب التاجر",
                "Vendor account reactivated",
                "تمت إعادة تشغيل حسابك وإرجاعه للحالة النشطة.",
                "Your vendor account has been reactivated and returned to active status.",
                "/dashboard",
                vendor.Id,
                SendPush: true),
            cancellationToken);
    }
}
