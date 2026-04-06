using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Vendors.Repositories;

public class VendorRepository : IVendorRepository
{
    private readonly ApplicationDbContext _dbContext;

    public VendorRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Vendor?> GetByIdAsync(Guid vendorId, CancellationToken cancellationToken = default) =>
        _dbContext.Vendors.FirstOrDefaultAsync(vendor => vendor.Id == vendorId, cancellationToken);

    public Task<Vendor?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Vendors.FirstOrDefaultAsync(vendor => vendor.UserId == userId, cancellationToken);

    public Task<bool> ExistsAsync(Guid vendorId, CancellationToken cancellationToken = default) =>
        _dbContext.Vendors.AnyAsync(vendor => vendor.Id == vendorId, cancellationToken);

    public void Add(Vendor vendor) => _dbContext.Vendors.Add(vendor);

    public void AddBranch(VendorBranch branch) => _dbContext.VendorBranches.Add(branch);

    public void AddBankAccount(VendorBankAccount bankAccount) => _dbContext.VendorBankAccounts.Add(bankAccount);
}
