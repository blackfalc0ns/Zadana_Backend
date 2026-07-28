using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Support;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Queries.AdminCustomers;

public record GetAdminCustomerStatsQuery : IRequest<AdminCustomerStatsDto>;

public class GetAdminCustomerStatsQueryHandler : IRequestHandler<GetAdminCustomerStatsQuery, AdminCustomerStatsDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminCustomerStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminCustomerStatsDto> Handle(GetAdminCustomerStatsQuery request, CancellationToken cancellationToken)
    {
        var orderStats = await _context.Orders
            .AsNoTracking()
            .GroupBy(order => order.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                TotalOrders = group.Count(),
                TotalSpent = group.Sum(order => order.TotalAmount),
                LastOrderAtUtc = group.Max(order => (DateTime?)order.PlacedAtUtc),
                RefundedOrdersCount = group.Count(order =>
                    order.Status == Domain.Modules.Orders.Enums.OrderStatus.Refunded ||
                    order.PaymentStatus == Domain.Modules.Payments.Enums.PaymentStatus.Refunded ||
                    order.PaymentStatus == Domain.Modules.Payments.Enums.PaymentStatus.PartiallyRefunded)
            })
            .ToDictionaryAsync(item => item.UserId, cancellationToken);

        var customers = await _context.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Customer)
            .Select(user => new
            {
                user.Id,
                user.AccountStatus,
                user.IsLoginLocked,
                user.CreatedAtUtc,
                user.LastLoginAtUtc
            })
            .ToListAsync(cancellationToken);

        var rows = customers.Select(customer =>
        {
            orderStats.TryGetValue(customer.Id, out var stats);

            return new AdminCustomerStatsCalculator.CustomerStatsRow(
                customer.Id,
                customer.AccountStatus,
                customer.IsLoginLocked,
                customer.CreatedAtUtc,
                customer.LastLoginAtUtc,
                stats?.LastOrderAtUtc,
                stats?.TotalOrders ?? 0,
                stats?.TotalSpent ?? 0m,
                stats?.RefundedOrdersCount ?? 0);
        });

        return AdminCustomerStatsCalculator.Calculate(rows);
    }
}
