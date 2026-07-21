using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorFinanceSettings;

public record AdminUpdateVendorFinanceSettingsCommand(
    Guid VendorId,
    string FinancialLifecycleMode,
    string? PayoutCycle,
    string? PayoutDay = null) : IRequest<VendorDetailDto>;

public class AdminUpdateVendorFinanceSettingsCommandValidator : AbstractValidator<AdminUpdateVendorFinanceSettingsCommand>
{
    private static readonly string[] AllowedModes =
    [
        "weekly",
        "biweekly",
        "monthly"
    ];

    public AdminUpdateVendorFinanceSettingsCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.FinancialLifecycleMode)
            .NotEmpty().WithMessage(x => localizer["Required"])
            .Must(mode => AllowedModes.Contains(mode.Trim().ToLowerInvariant()))
            .WithMessage(x => localizer["InvalidValue"]);

        RuleFor(x => x.PayoutCycle)
            .MaximumLength(50)
            .Must(IsSupportedPayoutCycle)
            .When(x => !string.IsNullOrWhiteSpace(x.PayoutCycle))
            .WithMessage(x => localizer["MaxLength"]);

        RuleFor(x => x.PayoutDay)
            .Must(value => string.IsNullOrWhiteSpace(value) || PayoutScheduleDayPolicy.TryParse(value, out _))
            .WithMessage(x => localizer["InvalidValue"]);
    }

    private static bool IsSupportedPayoutCycle(string? payoutCycle) =>
        string.IsNullOrWhiteSpace(payoutCycle) ||
        payoutCycle.Trim().ToLowerInvariant() is "weekly" or "biweekly" or "monthly";
}

public class AdminUpdateVendorFinanceSettingsCommandHandler : IRequestHandler<AdminUpdateVendorFinanceSettingsCommand, VendorDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IVendorReadService _vendorReadService;
    private readonly IVendorCommunicationService _vendorCommunicationService;
    private readonly ISettlementProcessingSettingsService _settlementProcessingSettingsService;

    public AdminUpdateVendorFinanceSettingsCommandHandler(
        IApplicationDbContext context,
        IVendorReadService vendorReadService,
        IVendorCommunicationService vendorCommunicationService,
        ISettlementProcessingSettingsService settlementProcessingSettingsService)
    {
        _context = context;
        _vendorReadService = vendorReadService;
        _vendorCommunicationService = vendorCommunicationService;
        _settlementProcessingSettingsService = settlementProcessingSettingsService;
    }

    public async Task<VendorDetailDto> Handle(AdminUpdateVendorFinanceSettingsCommand request, CancellationToken cancellationToken)
    {
        var vendor = await _context.Vendors
            .Include(item => item.BankAccounts)
            .FirstOrDefaultAsync(item => item.Id == request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        var mode = ParseMode(request.FinancialLifecycleMode);

        if (mode == VendorFinancialLifecycleMode.PerOrderDirectPayout)
        {
            throw new BusinessRuleException(
                "PER_ORDER_DIRECT_PAYOUT_UNAVAILABLE",
                "Per-order direct payout is not available. Choose a scheduled payout cycle.");
        }

        var payoutDay = await _settlementProcessingSettingsService.ResolveConfiguredPayoutDayAsync(
            request.PayoutDay,
            vendor.PayoutDay,
            cancellationToken);

        vendor.UpdateFinanceSettings(
            mode,
            request.PayoutCycle,
            payoutDay);
        await _context.SaveChangesAsync(cancellationToken);

        await _vendorCommunicationService.SendAsync(
            vendor,
            new VendorCommunicationMessage(
                "vendor_finance_settings_updated",
                "حدّثنا إعدادات الدورة المالية",
                "Vendor finance settings updated",
                "حدّثنا إعدادات الدورة المالية والتحويلات من لوحة الإدارة.",
                "Your payout and finance lifecycle settings were updated by the admin team.",
                "/finance",
                vendor.Id),
            cancellationToken);

        return await _vendorReadService.GetDetailAsync(request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);
    }

    private static VendorFinancialLifecycleMode ParseMode(string mode) =>
        mode.Trim().ToLowerInvariant() switch
        {
            "biweekly" => VendorFinancialLifecycleMode.Biweekly,
            "monthly" => VendorFinancialLifecycleMode.Monthly,
            "per_order_direct_payout" => VendorFinancialLifecycleMode.PerOrderDirectPayout,
            _ => VendorFinancialLifecycleMode.Weekly
        };
}
