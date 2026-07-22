using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Wallets.Commands.EscalateVendorPayout;

public record EscalateVendorPayoutCommand(Guid VendorId, Guid PayoutId) : IRequest<Guid>;

public class EscalateVendorPayoutCommandValidator : AbstractValidator<EscalateVendorPayoutCommand>
{
    public EscalateVendorPayoutCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.PayoutId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
    }
}

public class EscalateVendorPayoutCommandHandler : IRequestHandler<EscalateVendorPayoutCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly VendorPayoutWalletService _vendorPayoutWalletService;
    private readonly IAdminAlertService _adminAlertService;

    public EscalateVendorPayoutCommandHandler(
        IApplicationDbContext context,
        VendorPayoutWalletService vendorPayoutWalletService,
        IAdminAlertService adminAlertService)
    {
        _context = context;
        _vendorPayoutWalletService = vendorPayoutWalletService;
        _adminAlertService = adminAlertService;
    }

    public async Task<Guid> Handle(EscalateVendorPayoutCommand request, CancellationToken cancellationToken)
    {
        var payout = await _context.Payouts
            .Include(item => item.Settlement)
            .FirstOrDefaultAsync(
                item => item.Id == request.PayoutId && item.Settlement.VendorId == request.VendorId,
                cancellationToken);

        if (payout is null)
        {
            throw new InvalidOperationException("Vendor payout was not found.");
        }

        if (payout.Status == PayoutStatus.Paid)
        {
            throw new BusinessRuleException("PAYOUT_INVALID_STATUS", "Paid payouts cannot be escalated.");
        }

        payout.MarkAsFailed();
        if (payout.Settlement.Status is not SettlementStatus.Settled)
        {
            payout.Settlement.MarkAsFailed();
        }

        await _vendorPayoutWalletService.ReleaseHoldAsync(
            request.VendorId,
            payout.SettlementId,
            payout.Amount,
            "PayoutFailedRelease",
            $"Hold released after payout failure {payout.Id}",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.SettlementFailed,
                AdminAlertCategories.Settlements,
                AdminAlertPriorities.Critical,
                "فشل تحويل التاجر",
                "Vendor payout failed",
                $"فشل التحويل رقم {payout.Id} للتسوية رقم {payout.SettlementId} ويحتاج إلى مراجعة.",
                $"Payout {payout.Id} failed and settlement {payout.SettlementId} needs review.",
                payout.SettlementId,
                $"/finances/settlements?focus={payout.SettlementId:D}&payoutId={payout.Id:D}",
                new
                {
                    vendorId = request.VendorId,
                    payoutId = payout.Id,
                    settlementId = payout.SettlementId,
                    amount = payout.Amount
                }),
            cancellationToken);

        return payout.Id;
    }
}
