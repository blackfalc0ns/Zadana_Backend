using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Application.Modules.Vendors.Interfaces;

public interface IVendorRepository
{
    Task<Vendor?> GetByIdAsync(Guid vendorId, CancellationToken cancellationToken = default);
    Task<Vendor?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid vendorId, CancellationToken cancellationToken = default);
    Task<VendorBranch?> GetPrimaryBranchAsync(Guid vendorId, CancellationToken cancellationToken = default);
    Task<VendorBankAccount?> GetPrimaryBankAccountAsync(Guid vendorId, CancellationToken cancellationToken = default);
    void Add(Vendor vendor);
    void AddBranch(VendorBranch branch);
    void AddBankAccount(VendorBankAccount bankAccount);
}
