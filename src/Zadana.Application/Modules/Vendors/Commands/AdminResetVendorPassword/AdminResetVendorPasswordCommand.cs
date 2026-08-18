using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminResetVendorPassword;

public record AdminResetVendorPasswordCommand(Guid VendorId, string NewPassword) : IRequest;

public class AdminResetVendorPasswordCommandValidator : AbstractValidator<AdminResetVendorPasswordCommand>
{
    public AdminResetVendorPasswordCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(localizer["RequiredField"].Value)
            .MinimumLength(8).WithMessage(localizer["PasswordMinLength"].Value)
            .Matches("[a-z]").WithMessage(localizer["PasswordComplexity"].Value)
            .Matches("[0-9]").WithMessage(localizer["PasswordComplexity"].Value)
            .WithName(localizer["NewPassword"].Value);
    }
}

public class AdminResetVendorPasswordCommandHandler : IRequestHandler<AdminResetVendorPasswordCommand>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AdminResetVendorPasswordCommandHandler(
        IVendorRepository vendorRepository,
        IIdentityAccountService identityAccountService,
        IRefreshTokenStore refreshTokenStore,
        IVendorReviewAuditService vendorReviewAuditService,
        IVendorCommunicationService vendorCommunicationService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _vendorRepository = vendorRepository;
        _identityAccountService = identityAccountService;
        _refreshTokenStore = refreshTokenStore;
        _vendorReviewAuditService = vendorReviewAuditService;
        _vendorCommunicationService = vendorCommunicationService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task Handle(AdminResetVendorPasswordCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        var resetResult = await _identityAccountService.ResetPasswordByAdminAsync(vendor.UserId, request.NewPassword, cancellationToken);
        if (!resetResult.Succeeded)
        {
            var details = string.Join(" ", resetResult.Errors ?? []);
            throw new BusinessRuleException(
                "IDENTITY_RESET_PASSWORD_FAILED",
                string.IsNullOrWhiteSpace(details)
                    ? "فشل إعادة تعيين كلمة المرور. لازم 8 أحرف على الأقل مع حرف إنجليزي صغير ورقم.|Password reset failed. Use at least 8 characters with a lowercase letter and a number."
                    : details);
        }

        await _refreshTokenStore.RevokeAllByUserAsync(vendor.UserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            "password-reset",
            "warning",
            "أعدنا تعيين كلمة مرور التاجر من الإدارة وألغينا جميع الجلسات النشطة.",
            "التحكم الأمني",
            "المسؤول",
            _currentUserService.UserId,
            cancellationToken: cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_password_reset",
                "أعدنا تعيين كلمة مرور حسابك",
                "Vendor password reset",
                "أعدنا تعيين كلمة مرور حسابك من الإدارة، وأنهينا الجلسات المفتوحة.",
                "Your password was reset by the admin team and active sessions were revoked.",
                "/login",
                vendor.Id,
                SendPush: true),
            cancellationToken);
    }
}
