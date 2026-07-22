using FluentValidation;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Application.Modules.Wallets.Commands.CreateSettlement;

public record CreateSettlementCommand(
    Guid? VendorId,
    Guid? DriverId,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal NetAmount,
    SettlementOrigin Origin = SettlementOrigin.ManualBatch) : MediatR.IRequest<Guid>;

public class CreateSettlementCommandValidator : AbstractValidator<CreateSettlementCommand>
{
    public CreateSettlementCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x)
            .Must(x => x.VendorId.HasValue || x.DriverId.HasValue)
            .WithMessage(x => localizer["EitherVendorOrDriverRequired"]);

        RuleFor(x => x.GrossAmount)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);

        RuleFor(x => x.CommissionAmount)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);

        RuleFor(x => x.NetAmount)
            .GreaterThanOrEqualTo(0).WithMessage(x => localizer["MinValue"]);

        RuleFor(x => x)
            .Must(x => Math.Abs((x.GrossAmount - x.CommissionAmount) - x.NetAmount) <= 0.01m)
            .WithMessage(x => localizer["InvalidAmount"]);
    }
}

public class CreateSettlementCommandHandler : IRequestHandler<CreateSettlementCommand, Guid>
{
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
        var settlement = new Settlement(request.VendorId, request.DriverId, request.Origin);
        settlement.UpdateTotals(request.GrossAmount, request.CommissionAmount);

        _context.Settlements.Add(settlement);
        await _context.SaveChangesAsync(cancellationToken);

        if (request.VendorId.HasValue)
        {
            var primaryBankAccount = await _context.VendorBankAccounts
                .AsNoTracking()
                .Where(item => item.VendorId == request.VendorId.Value)
                .OrderByDescending(item => item.IsPrimary)
                .ThenByDescending(item => item.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (primaryBankAccount is not null)
            {
                var payout = new Payout(settlement.Id, settlement.NetAmount, primaryBankAccount.Id);
                payout.PrepareDestination(
                    PayoutDestinationType.VendorBankAccount,
                    PayoutDestinationSnapshotCodec.CreateVendorBankAccount(primaryBankAccount));
                var payoutDay = await _context.Vendors
                    .AsNoTracking()
                    .Where(item => item.Id == request.VendorId.Value)
                    .Select(item => (PayoutScheduleDay?)item.PayoutDay)
                    .FirstOrDefaultAsync(cancellationToken);
                if (payoutDay.HasValue)
                {
                    payout.SetScheduledPayoutDay(payoutDay.Value);
                }
                _context.Payouts.Add(payout);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        await _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.SettlementRequested,
                AdminAlertCategories.Settlements,
                AdminAlertPriorities.High,
                "تسوية جديدة تحتاج إلى مراجعة",
                "Settlement requires finance review",
                $"تم إنشاء التسوية رقم {settlement.Id} بصافي مبلغ {settlement.NetAmount:0.##} ر.س.",
                $"Settlement {settlement.Id} was created with net amount {settlement.NetAmount:0.##}.",
                settlement.Id,
                $"/finances/settlements?focus={settlement.Id:D}",
                new
                {
                    settlementId = settlement.Id,
                    vendorId = request.VendorId,
                    driverId = request.DriverId,
                    netAmount = settlement.NetAmount,
                    origin = request.Origin.ToString()
                }),
            cancellationToken);

        return settlement.Id;
    }
}
