using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UnlockVendorLogin;

public record UnlockVendorLoginCommand(Guid VendorId) : IRequest;

public class UnlockVendorLoginCommandValidator : AbstractValidator<UnlockVendorLoginCommand>
{
    public UnlockVendorLoginCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty();
    }
}

public class UnlockVendorLoginCommandHandler : IRequestHandler<UnlockVendorLoginCommand>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UnlockVendorLoginCommandHandler(
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

    public async Task Handle(UnlockVendorLoginCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        vendor.Unlock();

        var unlockResult = await _identityAccountService.UnlockLoginAsync(vendor.UserId, cancellationToken);
        if (!unlockResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_UNLOCK_FAILED", string.Join(", ", unlockResult.Errors ?? []));
        }

        var activateResult = await _identityAccountService.ActivateAsync(vendor.UserId, cancellationToken);
        if (!activateResult.Succeeded)
        {
            throw new BusinessRuleException("IDENTITY_ACTIVATE_FAILED", string.Join(", ", activateResult.Errors ?? []));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            "login-unlocked",
            "success",
            "Vendor login was unlocked and account access was restored.",
            "Security Control",
            "Admin",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_login_unlocked",
                "تم فتح دخول حساب التاجر",
                "Vendor login unlocked",
                "تم فتح دخول حسابك واستعادة الوصول.",
                "Your login has been unlocked and account access was restored.",
                "/dashboard",
                vendor.Id,
                SendPush: true),
            cancellationToken);
    }
}
