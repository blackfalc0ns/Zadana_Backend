using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Application.Modules.Wallets.Commands.CreatePayout;

public class CreatePayoutCommandHandler : IRequestHandler<CreatePayoutCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreatePayoutCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreatePayoutCommand request, CancellationToken cancellationToken)
    {
        var settlement = await _context.Settlements
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.SettlementId, cancellationToken);

        if (settlement is null)
        {
            throw new InvalidOperationException("Settlement was not found.");
        }

        var bankAccount = await _context.VendorBankAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.VendorBankAccountId, cancellationToken);

        if (bankAccount is null)
        {
            throw new InvalidOperationException("Vendor bank account was not found.");
        }

        if (settlement.VendorId.HasValue && bankAccount.VendorId != settlement.VendorId.Value)
        {
            throw new InvalidOperationException("Vendor bank account does not belong to the settlement vendor.");
        }

        var payout = new Payout(request.SettlementId, request.Amount, request.VendorBankAccountId);

        _context.Payouts.Add(payout);
        await _context.SaveChangesAsync(cancellationToken);

        return payout.Id;
    }
}
