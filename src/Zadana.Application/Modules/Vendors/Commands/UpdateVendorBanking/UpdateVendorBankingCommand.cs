using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorBanking;

public record UpdateVendorBankingCommand(
    string BankName,
    string AccountHolderName,
    string Iban,
    string? SwiftCode,
    string? PayoutCycle) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorBankingCommandValidator : AbstractValidator<UpdateVendorBankingCommand>
{
    public UpdateVendorBankingCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountHolderName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Iban).NotEmpty().MaximumLength(34);
        RuleFor(x => x.SwiftCode).MaximumLength(11);
        RuleFor(x => x.PayoutCycle).MaximumLength(50);
    }
}

public class UpdateVendorBankingCommandHandler : IRequestHandler<UpdateVendorBankingCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IProfileChangeApprovalService _profileChangeApprovalService;

    public UpdateVendorBankingCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        ICurrentUserService currentUserService,
        IVendorReviewAuditService vendorReviewAuditService,
        IProfileChangeApprovalService profileChangeApprovalService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _currentUserService = currentUserService;
        _vendorReviewAuditService = vendorReviewAuditService;
        _profileChangeApprovalService = profileChangeApprovalService;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorBankingCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        var payload = new VendorBankingProfileChangePayload(
            vendor.Id,
            request.BankName,
            request.AccountHolderName,
            request.Iban,
            request.SwiftCode,
            request.PayoutCycle);

        await _profileChangeApprovalService.SubmitAsync(
            userId,
            vendor.UserId,
            ProfileChangeApprovalActions.VendorProfileBanking,
            $"Vendor {vendor.BusinessNameEn} requested banking profile changes.",
            payload,
            new ProfileChangeApprovalAlert(
                AdminAlertTypes.VendorCriticalChangeSubmitted,
                AdminAlertCategories.Vendors,
                AdminAlertPriorities.High,
                "تعديل حساب تسويات بانتظار الموافقة",
                "Vendor banking change pending approval",
                $"أرسل التاجر {vendor.BusinessNameAr} تعديل حساب التسويات وينتظر موافقة الأدمن.",
                $"Vendor {vendor.BusinessNameEn} submitted banking changes pending admin approval.",
                vendor.Id,
                "/admin/access/approvals",
                new { vendorId = vendor.Id, userId = vendor.UserId, section = "banking" }),
            cancellationToken);

        await _vendorReviewAuditService.AppendActivityEntryAsync(
            vendor.UserId,
            "profile-banking-change-submitted",
            "warning",
            "أرسل التاجر تعديل الحساب البنكي للمراجعة قبل التطبيق.",
            "بوابة التاجر",
            vendor.BusinessNameAr,
            userId,
            vendor.BusinessNameAr,
            cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
