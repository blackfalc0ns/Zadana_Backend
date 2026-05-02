using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Wallets.Commands.CompleteVendorPayout;

public record CompleteVendorPayoutCommand(Guid VendorId, Guid PayoutId, string? TransferReference) : IRequest<Guid>;

public class CompleteVendorPayoutCommandValidator : AbstractValidator<CompleteVendorPayoutCommand>
{
    public CompleteVendorPayoutCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.PayoutId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.TransferReference)
            .MaximumLength(200)
            .WithMessage(x => localizer["MaxLength"]);
    }
}

public class CompleteVendorPayoutCommandHandler : IRequestHandler<CompleteVendorPayoutCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly VendorPayoutWalletService _vendorPayoutWalletService;

    public CompleteVendorPayoutCommandHandler(
        IApplicationDbContext context,
        VendorPayoutWalletService vendorPayoutWalletService)
    {
        _context = context;
        _vendorPayoutWalletService = vendorPayoutWalletService;
    }

    public async Task<Guid> Handle(CompleteVendorPayoutCommand request, CancellationToken cancellationToken)
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
            return payout.Id;
        }

        if (payout.Status is PayoutStatus.Cancelled or PayoutStatus.Failed)
        {
            throw new BusinessRuleException("PAYOUT_INVALID_STATUS", $"Cannot complete payout from status {payout.Status}.");
        }

        payout.MarkAsPaid(string.IsNullOrWhiteSpace(request.TransferReference)
            ? $"MANUAL-{payout.Id.ToString("N")[..8].ToUpperInvariant()}"
            : request.TransferReference.Trim());
        payout.Settlement.MarkAsSettled();

        await _vendorPayoutWalletService.SettleHoldAsync(
            request.VendorId,
            payout.SettlementId,
            payout.Id,
            payout.Amount,
            $"Vendor payout completed {payout.Id}",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return payout.Id;
    }
}
