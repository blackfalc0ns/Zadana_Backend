using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Wallets.Commands.SuspendVendorPayout;

public record SuspendVendorPayoutCommand(Guid VendorId, Guid PayoutId) : IRequest<Guid>;

public class SuspendVendorPayoutCommandValidator : AbstractValidator<SuspendVendorPayoutCommand>
{
    public SuspendVendorPayoutCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.PayoutId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
    }
}

public class SuspendVendorPayoutCommandHandler : IRequestHandler<SuspendVendorPayoutCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly PayoutOrchestrator _payoutOrchestrator;
    private readonly IAdminAlertService _adminAlertService;

    public SuspendVendorPayoutCommandHandler(
        IApplicationDbContext context,
        PayoutOrchestrator payoutOrchestrator,
        IAdminAlertService adminAlertService)
    {
        _context = context;
        _payoutOrchestrator = payoutOrchestrator;
        _adminAlertService = adminAlertService;
    }

    public async Task<Guid> Handle(SuspendVendorPayoutCommand request, CancellationToken cancellationToken)
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

        if (payout.Status is PayoutStatus.Paid or PayoutStatus.Reversed)
        {
            throw new BusinessRuleException("PAYOUT_INVALID_STATUS", "Paid payouts cannot be suspended.");
        }

        // Route every suspension through the orchestrator so an in-flight
        // gateway payout or a submitted manual transfer cannot be silently
        // cancelled while its bank operation may still complete.
        await _payoutOrchestrator.CancelAsync(payout.Id, cancellationToken);

        await _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.SettlementFailed,
                AdminAlertCategories.Settlements,
                AdminAlertPriorities.High,
                "تم تعليق تحويل التاجر",
                "Vendor payout suspended",
                $"تم تعليق التحويل رقم {payout.Id} للتسوية رقم {payout.SettlementId} ويحتاج إلى مراجعة.",
                $"Payout {payout.Id} was suspended and settlement {payout.SettlementId} needs review.",
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
