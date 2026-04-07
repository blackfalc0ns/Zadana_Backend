using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Wallets.DTOs;

namespace Zadana.Application.Modules.Wallets.Queries.GetVendorSettlements;

public record GetVendorSettlementsQuery(
    Guid VendorId,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedList<AdminVendorSettlementDto>>;

public class GetVendorSettlementsQueryHandler : IRequestHandler<GetVendorSettlementsQuery, PaginatedList<AdminVendorSettlementDto>>
{
    private readonly IApplicationDbContext _context;

    public GetVendorSettlementsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<AdminVendorSettlementDto>> Handle(GetVendorSettlementsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Settlements
            .AsNoTracking()
            .Where(item => item.VendorId == request.VendorId);

        var totalCount = await query.CountAsync(cancellationToken);

        var settlements = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new
            {
                item.Id,
                item.GrossAmount,
                item.CommissionAmount,
                item.NetAmount,
                Status = item.Status.ToString(),
                item.CreatedAtUtc,
                item.ProcessedAtUtc,
                PayoutsCount = item.Payouts.Count
            })
            .ToListAsync(cancellationToken);

        var settlementDtos = settlements
            .Select(item => new AdminVendorSettlementDto(
                item.Id,
                $"SET-{item.Id.ToString("N")[..8].ToUpperInvariant()}",
                item.GrossAmount,
                item.CommissionAmount,
                item.NetAmount,
                item.Status,
                item.CreatedAtUtc,
                item.ProcessedAtUtc,
                item.PayoutsCount))
            .ToList();

        return new PaginatedList<AdminVendorSettlementDto>(settlementDtos, totalCount, request.Page, request.PageSize);
    }
}
