using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.AddVendorBankAccount;

public class AddVendorBankAccountCommandHandler : IRequestHandler<AddVendorBankAccountCommand, Guid>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddVendorBankAccountCommandHandler(IVendorRepository vendorRepository, IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(AddVendorBankAccountCommand request, CancellationToken cancellationToken)
    {
        var vendorExists = await _vendorRepository.ExistsAsync(request.VendorId, cancellationToken);
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
        _vendorRepository.AddBankAccount(bankAccount);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return bankAccount.Id;
    }
}
