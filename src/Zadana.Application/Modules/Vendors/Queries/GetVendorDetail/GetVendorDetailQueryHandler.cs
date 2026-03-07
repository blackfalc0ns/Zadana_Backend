using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Queries.GetVendorDetail;

public class GetVendorDetailQueryHandler : IRequestHandler<GetVendorDetailQuery, VendorDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetVendorDetailQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<VendorDetailDto> Handle(GetVendorDetailQuery request, CancellationToken cancellationToken)
    {
        var vendor = await _db.Vendors
            .Include(v => v.User)
            .Include(v => v.Branches)
            .Include(v => v.BankAccounts)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Vendor", request.VendorId);

        return new VendorDetailDto(
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
            // Owner info
            vendor.User.FullName,
            vendor.User.Email,
            vendor.User.PhoneNumber,
            // Counts
            vendor.Branches.Count,
            vendor.BankAccounts.Count);
    }
}
