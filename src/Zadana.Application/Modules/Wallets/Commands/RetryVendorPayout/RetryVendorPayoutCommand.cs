using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Wallets.Commands.RetryVendorPayout;

public record RetryVendorPayoutCommand(Guid VendorId, Guid PayoutId) : IRequest<Guid>;

public class RetryVendorPayoutCommandValidator : AbstractValidator<RetryVendorPayoutCommand>
{
    public RetryVendorPayoutCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.VendorId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
        RuleFor(x => x.PayoutId).NotEmpty().WithMessage(x => localizer["RequiredField"]);
    }
}

public class RetryVendorPayoutCommandHandler : IRequestHandler<RetryVendorPayoutCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public RetryVendorPayoutCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(RetryVendorPayoutCommand request, CancellationToken cancellationToken)
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

        if (payout.Status is not PayoutStatus.Failed
            && payout.Status is not PayoutStatus.Cancelled)
        {
            throw new BusinessRuleException("PAYOUT_INVALID_STATUS", $"Cannot retry payout from status {payout.Status}.");
        }

        payout.MarkAsProcessing();
        await _context.SaveChangesAsync(cancellationToken);

        return payout.Id;
    }
}
