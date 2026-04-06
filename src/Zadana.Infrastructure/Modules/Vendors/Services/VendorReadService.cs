using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Vendors.Services;

public class VendorReadService : IVendorReadService
{
    private readonly ApplicationDbContext _dbContext;

    public VendorReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<VendorListItemDto>> GetAllAsync(
        VendorStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query =
            from vendor in _dbContext.Vendors.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking() on vendor.UserId equals user.Id
            select new { vendor, user };

        if (status.HasValue)
        {
            query = query.Where(item => item.vendor.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(item =>
                EF.Functions.Like(item.vendor.BusinessNameAr, pattern) ||
                EF.Functions.Like(item.vendor.BusinessNameEn, pattern) ||
                EF.Functions.Like(item.vendor.ContactPhone, pattern) ||
                EF.Functions.Like(item.vendor.ContactEmail, pattern) ||
                EF.Functions.Like(item.user.FullName, pattern));
        }

        var projected = query
            .OrderByDescending(item => item.vendor.CreatedAtUtc)
            .Select(item => new VendorListItemDto(
                item.vendor.Id,
                item.vendor.BusinessNameAr,
                item.vendor.BusinessNameEn,
                item.vendor.BusinessType,
                item.vendor.Status.ToString(),
                item.user.FullName,
                item.vendor.ContactPhone,
                item.vendor.CreatedAtUtc,
                item.vendor.ContactEmail,
                item.vendor.CommissionRate));

        return await PaginatedList<VendorListItemDto>.CreateAsync(projected, page, pageSize, cancellationToken);
    }

    public Task<VendorDetailDto?> GetDetailAsync(Guid vendorId, CancellationToken cancellationToken = default) =>
        (from vendor in _dbContext.Vendors.AsNoTracking()
         join user in _dbContext.Users.AsNoTracking() on vendor.UserId equals user.Id
         where vendor.Id == vendorId
         select new VendorDetailDto(
             vendor.Id,
             vendor.BusinessNameAr,
             vendor.BusinessNameEn,
             vendor.BusinessType,
             vendor.CommercialRegistrationNumber,
             vendor.TaxId,
             vendor.ContactEmail,
             vendor.ContactPhone,
             vendor.CommissionRate,
             vendor.Status.ToString(),
             vendor.RejectionReason,
             vendor.LogoUrl,
             vendor.CommercialRegisterDocumentUrl,
             vendor.ApprovedAtUtc,
             vendor.ApprovedBy,
             vendor.CreatedAtUtc,
             user.FullName,
             user.Email ?? string.Empty,
             user.PhoneNumber ?? string.Empty,
             _dbContext.VendorBranches.Count(branch => branch.VendorId == vendor.Id),
             _dbContext.VendorBankAccounts.Count(account => account.VendorId == vendor.Id)))
        .FirstOrDefaultAsync(cancellationToken);

    public Task<VendorProfileDto?> GetProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Vendors
            .AsNoTracking()
            .Where(vendor => vendor.UserId == userId)
            .Select(vendor => new VendorProfileDto(
                vendor.Id,
                vendor.BusinessNameAr,
                vendor.BusinessNameEn,
                vendor.BusinessType,
                vendor.CommercialRegistrationNumber,
                vendor.TaxId,
                vendor.ContactEmail,
                vendor.ContactPhone,
                vendor.CommissionRate,
                vendor.Status.ToString(),
                vendor.LogoUrl,
                vendor.ApprovedAtUtc,
                vendor.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Guid?> GetVendorIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Vendors
            .AsNoTracking()
            .Where(vendor => vendor.UserId == userId)
            .Select(vendor => (Guid?)vendor.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
