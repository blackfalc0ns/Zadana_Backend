using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Wallets.DTOs;

namespace Zadana.Application.Modules.Wallets.Queries.GetVendorPayouts;

public record GetVendorPayoutsQuery(
    Guid VendorId,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedList<AdminVendorPayoutDto>>;

public class GetVendorPayoutsQueryHandler : IRequestHandler<GetVendorPayoutsQuery, PaginatedList<AdminVendorPayoutDto>>
{
    private readonly IApplicationDbContext _context;

    public GetVendorPayoutsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<AdminVendorPayoutDto>> Handle(GetVendorPayoutsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Payouts
            .AsNoTracking()
            .Include(item => item.Settlement)
            .Include(item => item.VendorBankAccount)
            .Where(item => item.Settlement.VendorId == request.VendorId);

        var totalCount = await query.CountAsync(cancellationToken);

        var payouts = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new
            {
                item.Id,
                item.SettlementId,
                item.Amount,
                Status = item.Status.ToString(),
                item.TransferReference,
                item.CreatedAtUtc,
                item.ProcessedAtUtc,
                item.VendorBankAccountId,
                BankName = item.VendorBankAccount != null ? item.VendorBankAccount.BankName : null,
                AccountHolderName = item.VendorBankAccount != null ? item.VendorBankAccount.AccountHolderName : null,
                Iban = item.VendorBankAccount != null ? item.VendorBankAccount.IBAN : null,
                SwiftCode = item.VendorBankAccount != null ? item.VendorBankAccount.SwiftCode : null
            })
            .ToListAsync(cancellationToken);

        var payoutDtos = payouts
            .Select(item => new AdminVendorPayoutDto(
                item.Id,
                item.SettlementId,
                $"PAY-{item.Id.ToString("N")[..8].ToUpperInvariant()}",
                item.Amount,
                item.Status,
                item.TransferReference,
                item.CreatedAtUtc,
                item.ProcessedAtUtc,
                item.VendorBankAccountId,
                item.BankName,
                item.AccountHolderName,
                item.Iban,
                item.SwiftCode))
            .ToList();

        return new PaginatedList<AdminVendorPayoutDto>(payoutDtos, totalCount, request.Page, request.PageSize);
    }
}
