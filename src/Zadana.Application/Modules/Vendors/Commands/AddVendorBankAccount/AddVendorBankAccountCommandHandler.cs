using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AddVendorBankAccount;

public class AddVendorBankAccountCommandHandler : IRequestHandler<AddVendorBankAccountCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public AddVendorBankAccountCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(AddVendorBankAccountCommand request, CancellationToken cancellationToken)
    {
        // 1. Check if vendor exists
        var vendorExists = _context.Vendors.Any(v => v.Id == request.VendorId);
        if (!vendorExists)
        {
            throw new NotFoundException("Vendor", request.VendorId);
        }

        // 2. Map Entity
        var bankAccount = new VendorBankAccount(
            vendorId: request.VendorId,
            bankName: request.BankName,
            accountHolderName: request.AccountHolderName,
            iban: request.Iban,
            swiftCode: request.SwiftCode
        );

        // Note: The command includes 'IsPrimary', but the domain logic requires
        // the account to be Verified before it can be set as primary. As a result,
        // it starts as 'PendingVerification' and 'IsPrimary' must be set later.

        // 3. Save to database
        _context.VendorBankAccounts.Add(bankAccount);
        await _context.SaveChangesAsync(cancellationToken);

        return bankAccount.Id;
    }
}
