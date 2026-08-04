using FluentValidation;
using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Support;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorBanking;

public record UpdateVendorBankingCommand(
    string BankName,
    string AccountHolderName,
    string Iban,
    string? SwiftCode,
    string? PayoutCycle,
    string? PayoutDay = null) : IRequest<VendorWorkspaceDto>;

public class UpdateVendorBankingCommandValidator : AbstractValidator<UpdateVendorBankingCommand>
{
    public UpdateVendorBankingCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountHolderName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Iban).NotEmpty().MaximumLength(34);
        RuleFor(x => x.SwiftCode).MaximumLength(11);
        RuleFor(x => x.PayoutCycle)
            .MaximumLength(50)
            .Must(IsSupportedPayoutCycle);
        RuleFor(x => x.PayoutDay)
            .Must(value => string.IsNullOrWhiteSpace(value) || PayoutScheduleDayPolicy.TryParse(value, out _));
    }

    private static bool IsSupportedPayoutCycle(string? payoutCycle) =>
        string.IsNullOrWhiteSpace(payoutCycle) ||
        payoutCycle.Trim().ToLowerInvariant() is "weekly" or "biweekly" or "monthly";
}

public class UpdateVendorBankingCommandHandler : IRequestHandler<UpdateVendorBankingCommand, VendorWorkspaceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IVendorReadService _vendorReadService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVendorReviewAuditService _vendorReviewAuditService;
    private readonly IProfileChangeApprovalService _profileChangeApprovalService;
    private readonly IAdminAlertService _adminAlertService;
    private readonly ISettlementProcessingSettingsService _settlementProcessingSettingsService;

    public UpdateVendorBankingCommandHandler(
        IVendorRepository vendorRepository,
        IVendorReadService vendorReadService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IVendorReviewAuditService vendorReviewAuditService,
        IProfileChangeApprovalService profileChangeApprovalService,
        IAdminAlertService adminAlertService,
        ISettlementProcessingSettingsService settlementProcessingSettingsService)
    {
        _vendorRepository = vendorRepository;
        _vendorReadService = vendorReadService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _vendorReviewAuditService = vendorReviewAuditService;
        _profileChangeApprovalService = profileChangeApprovalService;
        _adminAlertService = adminAlertService;
        _settlementProcessingSettingsService = settlementProcessingSettingsService;
    }

    public async Task<VendorWorkspaceDto> Handle(UpdateVendorBankingCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        if (!string.IsNullOrWhiteSpace(request.PayoutDay))
        {
            var payoutDay = PayoutScheduleDayPolicy.ParseOrDefault(request.PayoutDay);
            await _settlementProcessingSettingsService.EnsurePayoutDayEnabledAsync(
                payoutDay,
                cancellationToken);
        }

        if (VendorReviewWorkflow.IsProfileReviewResubmission(vendor, "banking"))
        {
            var payoutDay = !string.IsNullOrWhiteSpace(request.PayoutDay)
                ? PayoutScheduleDayPolicy.ParseOrDefault(request.PayoutDay)
                : (PayoutScheduleDay?)null;

            vendor.UpdateBanking(request.PayoutCycle, payoutDay);

            var primaryAccount = vendor.BankAccounts
                .FirstOrDefault(account => account.IsPrimary)
                ?? vendor.BankAccounts
                    .OrderByDescending(account => account.CreatedAtUtc)
                    .FirstOrDefault();

            foreach (var account in vendor.BankAccounts)
            {
                account.UnsetPrimary();
            }

            if (primaryAccount is null)
            {
                primaryAccount = new VendorBankAccount(
                    vendor.Id,
                    request.BankName,
                    request.AccountHolderName,
                    request.Iban,
                    request.SwiftCode);

                primaryAccount.MarkAsPreferredForSetup();
                _vendorRepository.AddBankAccount(primaryAccount);
            }
            else
            {
                primaryAccount.UpdateDetails(
                    request.BankName,
                    request.AccountHolderName,
                    request.Iban,
                    request.SwiftCode);
                primaryAccount.MarkAsPreferredForSetup();
            }

            VendorProfileReviewMutations.ResetSectionToSubmitted(vendor, "banking");
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _vendorReviewAuditService.AppendActivityEntryAsync(
                vendor.UserId,
                "profile-banking-updated-for-review",
                "warning",
                "Vendor banking details were updated and resubmitted for compliance review.",
                "Vendor portal",
                vendor.BusinessNameAr,
                userId,
                vendor.BusinessNameAr,
                cancellationToken);

            await VendorProfileSectionAdminAlerts.NotifySectionReviewAsync(
                _adminAlertService,
                vendor,
                "banking",
                cancellationToken);

            return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException("Vendor", userId);
        }

        var payload = new VendorBankingProfileChangePayload(
            vendor.Id,
            request.BankName,
            request.AccountHolderName,
            request.Iban,
            request.SwiftCode,
            request.PayoutCycle,
            request.PayoutDay);

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
                "تعديل حساب تسويات بانتظار الاعتماد",
                "Vendor banking change pending approval",
                $"أرسل التاجر {vendor.BusinessNameAr} تعديل حساب التسويات وينتظر اعتماد المشرف.",
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

        await VendorProfileSectionAdminAlerts.NotifySectionReviewAsync(
            _adminAlertService,
            vendor,
            "banking",
            cancellationToken);

        return await _vendorReadService.GetWorkspaceByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);
    }
}
