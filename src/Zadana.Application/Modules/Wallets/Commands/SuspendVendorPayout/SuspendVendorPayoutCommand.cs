using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
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

    public SuspendVendorPayoutCommandHandler(IApplicationDbContext context)
    {
        _context = context;
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

        if (payout.Status == PayoutStatus.Paid)
        {
            throw new BusinessRuleException("PAYOUT_INVALID_STATUS", "Paid payouts cannot be suspended.");
        }

        payout.Cancel();
        await _context.SaveChangesAsync(cancellationToken);

        return payout.Id;
    }
}
