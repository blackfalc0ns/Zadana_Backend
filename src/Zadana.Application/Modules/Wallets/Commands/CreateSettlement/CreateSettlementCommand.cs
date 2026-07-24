using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Serialization;

namespace Zadana.Application.Modules.Wallets.Commands.CreateSettlement;

public record CreateSettlementCommand(
    Guid? VendorId,
    Guid? DriverId,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal NetAmount,
    decimal RefundAmount = 0m,
    decimal AdjustmentAmount = 0m,
    DateTime? PeriodFrom = null,
    DateTime? PeriodTo = null,
    SettlementOrigin Origin = SettlementOrigin.ManualBatch) : MediatR.IRequest<Guid>;

public class CreateSettlementCommandValidator : AbstractValidator<CreateSettlementCommand>
{
    private const decimal MaxAmount = 10_000_000m;
    private const int MaxPeriodDays = 93;

    public CreateSettlementCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x)
            .Must(x => x.VendorId.HasValue ^ x.DriverId.HasValue)
            .WithMessage(_ => localizer["EitherVendorOrDriverRequired"]);

        RuleFor(x => x.GrossAmount)
            .GreaterThan(0).WithMessage(_ => "SETTLEMENT_GROSS_REQUIRED")
            .LessThanOrEqualTo(MaxAmount).WithMessage(_ => "SETTLEMENT_AMOUNT_TOO_LARGE");

        RuleFor(x => x.CommissionAmount)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["MinValue"])
            .LessThanOrEqualTo(MaxAmount).WithMessage(_ => "SETTLEMENT_AMOUNT_TOO_LARGE");

        RuleFor(x => x.RefundAmount)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["MinValue"])
            .LessThanOrEqualTo(MaxAmount).WithMessage(_ => "SETTLEMENT_AMOUNT_TOO_LARGE");

        RuleFor(x => x.AdjustmentAmount)
            .InclusiveBetween(-MaxAmount, MaxAmount).WithMessage(_ => "SETTLEMENT_AMOUNT_TOO_LARGE");

        RuleFor(x => x.NetAmount)
            .GreaterThan(0).WithMessage(_ => "SETTLEMENT_NET_REQUIRED")
            .LessThanOrEqualTo(MaxAmount).WithMessage(_ => "SETTLEMENT_AMOUNT_TOO_LARGE");

        RuleFor(x => x)
            .Must(x => Math.Abs((x.GrossAmount - x.CommissionAmount - x.RefundAmount + x.AdjustmentAmount) - x.NetAmount) <= 0.01m)
            .WithMessage(_ => "SETTLEMENT_NET_MISMATCH");

        RuleFor(x => x)
            .Must(x => x.CommissionAmount + x.RefundAmount <= x.GrossAmount + Math.Max(0m, x.AdjustmentAmount) + 0.01m)
            .WithMessage(_ => "SETTLEMENT_DEDUCTIONS_EXCEED_GROSS");

        RuleFor(x => x)
            .Must(HasValidPeriod)
            .WithMessage(_ => "SETTLEMENT_PERIOD_INVALID");

        RuleFor(x => x)
            .Must(x =>
            {
                var from = (x.PeriodFrom ?? SaudiTime.Today).Date;
                var to = (x.PeriodTo ?? SaudiTime.Today).Date;
                return (to - from).TotalDays + 1 <= MaxPeriodDays;
            })
            .WithMessage(_ => "SETTLEMENT_PERIOD_TOO_LONG");

        RuleFor(x => x)
            .Must(x => (x.PeriodTo ?? SaudiTime.Today).Date <= SaudiTime.Today)
            .WithMessage(_ => "SETTLEMENT_PERIOD_IN_FUTURE");

        RuleFor(x => x)
            .Must(x => (x.PeriodFrom ?? SaudiTime.Today).Date >= SaudiTime.Today.AddYears(-1))
            .WithMessage(_ => "SETTLEMENT_PERIOD_TOO_OLD");

        RuleFor(x => x)
            .Must(x => !x.DriverId.HasValue)
            .When(x => x.Origin == SettlementOrigin.ManualBatch)
            .WithMessage(_ => "DRIVER_WITHDRAWAL_WORKFLOW_REQUIRED");
    }

    private static bool HasValidPeriod(CreateSettlementCommand request)
    {
        var from = (request.PeriodFrom ?? SaudiTime.Today).Date;
        var to = (request.PeriodTo ?? SaudiTime.Today).Date;
        return from <= to;
    }
}

public class CreateSettlementCommandHandler : IRequestHandler<CreateSettlementCommand, Guid>
{
    private static readonly SettlementStatus[] BlockingStatuses =
    [
        SettlementStatus.Pending,
        SettlementStatus.PendingReview,
        SettlementStatus.Approved,
        SettlementStatus.OnHold,
        SettlementStatus.Processing,
        SettlementStatus.Disputed
    ];

    private readonly IApplicationDbContext _context;
    private readonly IAdminAlertService _adminAlertService;

    public CreateSettlementCommandHandler(
        IApplicationDbContext context,
        IAdminAlertService adminAlertService)
    {
        _context = context;
        _adminAlertService = adminAlertService;
    }

    public async Task<Guid> Handle(CreateSettlementCommand request, CancellationToken cancellationToken)
    {
        if (!request.VendorId.HasValue)
        {
            throw new BusinessRuleException(
                "DRIVER_WITHDRAWAL_WORKFLOW_REQUIRED",
                "Exceptional settlements can only be created for vendors.");
        }

        var vendorId = request.VendorId.Value;
        var vendorExists = await _context.Vendors
            .AsNoTracking()
            .AnyAsync(item => item.Id == vendorId, cancellationToken);

        if (!vendorExists)
        {
            throw new NotFoundException("Vendor", vendorId);
        }

        var periodFrom = (request.PeriodFrom ?? SaudiTime.Today).Date;
        var periodTo = (request.PeriodTo ?? SaudiTime.Today).Date;

        var overlappingSettlement = await _context.Settlements
            .AsNoTracking()
            .Where(settlement =>
                settlement.OwnerType == SettlementOwnerType.Vendor &&
                settlement.OwnerId == vendorId &&
                BlockingStatuses.Contains(settlement.Status) &&
                settlement.PeriodFrom <= periodTo &&
                settlement.PeriodTo >= periodFrom)
            .Select(settlement => settlement.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (overlappingSettlement != Guid.Empty)
        {
            throw new BusinessRuleException(
                "SETTLEMENT_PERIOD_OVERLAP",
                $"An existing settlement (ID: {overlappingSettlement}) already covers part of this period for this vendor.");
        }

        var primaryBankAccount = await _context.VendorBankAccounts
            .AsNoTracking()
            .Where(item =>
                item.VendorId == vendorId &&
                item.IsPrimary &&
                item.Status == BankAccountStatus.Verified)
            .OrderByDescending(item => item.VerifiedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException(
                "VENDOR_VERIFIED_BANK_ACCOUNT_REQUIRED",
                "Vendor must have a verified primary bank account before creating an exceptional settlement.");

        if (!IsValidSaudiIban(primaryBankAccount.IBAN))
        {
            throw new BusinessRuleException(
                "VENDOR_BANK_IBAN_INVALID",
                "Vendor primary bank account must be a valid Saudi IBAN before creating an exceptional settlement.");
        }

        var wallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.OwnerType == WalletOwnerType.Vendor && item.OwnerId == vendorId,
                cancellationToken)
            ?? throw new BusinessRuleException(
                "VENDOR_WALLET_REQUIRED",
                "Vendor wallet is required before creating an exceptional settlement.");

        var activeHolds = await _context.WalletHolds
            .AsNoTracking()
            .Where(hold =>
                hold.OwnerType == WalletOwnerType.Vendor &&
                hold.OwnerId == vendorId &&
                hold.Status == WalletHoldStatus.Active)
            .SumAsync(hold => (decimal?)hold.Amount, cancellationToken) ?? 0m;

        var availableBalance = Math.Max(0m, wallet.CurrentBalance - wallet.PendingBalance - activeHolds);
        if (availableBalance < request.NetAmount)
        {
            throw new BusinessRuleException(
                "INSUFFICIENT_VENDOR_BALANCE",
                $"Vendor available balance ({availableBalance}) is less than settlement net amount ({request.NetAmount}).");
        }

        var settlement = new Settlement(
            SettlementOwnerType.Vendor,
            vendorId,
            periodFrom,
            periodTo,
            request.Origin);
        settlement.UpdateTotals(
            request.GrossAmount,
            request.CommissionAmount,
            request.RefundAmount,
            request.AdjustmentAmount);

        _context.Settlements.Add(settlement);

        var payout = new Payout(settlement.Id, settlement.NetAmount, primaryBankAccount.Id);
        payout.PrepareDestination(
            PayoutDestinationType.VendorBankAccount,
            PayoutDestinationSnapshotCodec.CreateVendorBankAccount(primaryBankAccount));

        var payoutDay = await _context.Vendors
            .AsNoTracking()
            .Where(item => item.Id == vendorId)
            .Select(item => (PayoutScheduleDay?)item.PayoutDay)
            .FirstOrDefaultAsync(cancellationToken);
        if (payoutDay.HasValue)
        {
            payout.SetScheduledPayoutDay(payoutDay.Value);
        }

        _context.Payouts.Add(payout);
        await _context.SaveChangesAsync(cancellationToken);

        await _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.SettlementRequested,
                AdminAlertCategories.Settlements,
                AdminAlertPriorities.High,
                "تسوية استثنائية جديدة تحتاج إلى مراجعة",
                "Exceptional settlement requires finance review",
                $"تم إنشاء تسوية استثنائية رقم {settlement.Id} بصافي مبلغ {settlement.NetAmount:0.##} ر.س.",
                $"Exceptional settlement {settlement.Id} was created with net amount {settlement.NetAmount:0.##}.",
                settlement.Id,
                $"/finances/settlements?focus={settlement.Id:D}",
                new
                {
                    settlementId = settlement.Id,
                    vendorId = request.VendorId,
                    driverId = request.DriverId,
                    netAmount = settlement.NetAmount,
                    origin = request.Origin.ToString(),
                    periodFrom,
                    periodTo
                }),
            cancellationToken);

        return settlement.Id;
    }

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
