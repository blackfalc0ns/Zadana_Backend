using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AdminUpdateVendorFinanceSettings;

public record AdminUpdateVendorFinanceSettingsCommand(
    Guid VendorId,
    string FinancialLifecycleMode,
    string? PayoutCycle) : IRequest<VendorDetailDto>;

public class AdminUpdateVendorFinanceSettingsCommandValidator : AbstractValidator<AdminUpdateVendorFinanceSettingsCommand>
{
    private static readonly string[] AllowedModes =
    [
        "weekly",
        "biweekly",
        "monthly",
        "per_order_direct_payout"
    ];

    public AdminUpdateVendorFinanceSettingsCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.FinancialLifecycleMode)
            .NotEmpty().WithMessage(x => localizer["Required"])
            .Must(mode => AllowedModes.Contains(mode.Trim().ToLowerInvariant()))
            .WithMessage(x => localizer["InvalidValue"]);

        RuleFor(x => x.PayoutCycle)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.PayoutCycle))
            .WithMessage(x => localizer["MaxLength"]);
    }
}

public class AdminUpdateVendorFinanceSettingsCommandHandler : IRequestHandler<AdminUpdateVendorFinanceSettingsCommand, VendorDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IVendorReadService _vendorReadService;
    private readonly IVendorCommunicationService _vendorCommunicationService;

    public AdminUpdateVendorFinanceSettingsCommandHandler(
        IApplicationDbContext context,
        IVendorReadService vendorReadService,
        IVendorCommunicationService vendorCommunicationService)
    {
        _context = context;
        _vendorReadService = vendorReadService;
        _vendorCommunicationService = vendorCommunicationService;
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
            var primaryBankAccount = vendor.BankAccounts
                .OrderByDescending(item => item.IsPrimary)
                .ThenByDescending(item => item.VerifiedAtUtc)
                .ThenByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault();

            if (primaryBankAccount is null)
            {
                throw new BusinessRuleException(
                    "PrimaryBankAccountRequiredForDirectPayout",
                    "A primary bank account is required before enabling direct per-order payouts.");
            }

            if (primaryBankAccount.Status != BankAccountStatus.Verified)
            {
                throw new BusinessRuleException(
                    "VerifiedPrimaryBankAccountRequiredForDirectPayout",
                    "A verified primary bank account is required before enabling direct per-order payouts.");
            }

            if (!IsValidSaudiIban(primaryBankAccount.IBAN))
            {
                throw new BusinessRuleException(
                    "ValidSaudiIbanRequiredForDirectPayout",
                    "A valid Saudi IBAN is required before enabling direct per-order payouts.");
            }
        }

        vendor.UpdateFinanceSettings(mode, request.PayoutCycle);
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

    private static bool IsValidSaudiIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return false;
        }

        var clean = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return clean.Length == 24 &&
            clean.StartsWith("SA", StringComparison.OrdinalIgnoreCase) &&
            clean.Skip(2).All(char.IsDigit);
    }
}
