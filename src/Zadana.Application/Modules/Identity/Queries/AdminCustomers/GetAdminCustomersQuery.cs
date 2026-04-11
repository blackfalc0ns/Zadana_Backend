using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Application.Modules.Identity.Queries.AdminCustomers;

public record GetAdminCustomersQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 50) : IRequest<PaginatedList<AdminCustomerListItemDto>>;

public class GetAdminCustomersQueryHandler : IRequestHandler<GetAdminCustomersQuery, PaginatedList<AdminCustomerListItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICustomerPresenceService _customerPresenceService;

    public GetAdminCustomersQueryHandler(IApplicationDbContext context, ICustomerPresenceService customerPresenceService)
    {
        _context = context;
        _customerPresenceService = customerPresenceService;
    }

    public async Task<PaginatedList<AdminCustomerListItemDto>> Handle(GetAdminCustomersQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 250);

        var query = _context.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Customer);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(user =>
                user.FullName.Contains(search) ||
                (user.Email != null && user.Email.Contains(search)) ||
                (user.PhoneNumber != null && user.PhoneNumber.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var customers = await query
            .OrderByDescending(user => user.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new
            {
                user.Id,
                user.FullName,
                user.Email,
                Phone = user.PhoneNumber,
                user.AccountStatus,
                user.IsLoginLocked,
                user.EmailConfirmed,
                PhoneConfirmed = user.PhoneNumberConfirmed,
                user.CreatedAtUtc,
                user.LastLoginAtUtc,
                user.LastSeenAtUtc
            })
            .ToListAsync(cancellationToken);

        var customerIds = customers.Select(customer => customer.Id).ToArray();

        var addressLookup = await _context.CustomerAddresses
            .AsNoTracking()
            .Where(address => customerIds.Contains(address.UserId))
            .Select(address => new
            {
                address.UserId,
                address.City,
                address.Area,
                address.IsDefault
            })
            .ToListAsync(cancellationToken);

        var primaryAddressByUser = addressLookup
            .GroupBy(address => address.UserId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(address => address.IsDefault)
                    .First());

        var orderStats = await _context.Orders
            .AsNoTracking()
            .Where(order => customerIds.Contains(order.UserId))
            .GroupBy(order => order.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                TotalOrders = group.Count(),
                TotalSpent = group.Sum(order => order.TotalAmount),
                AverageBasket = group.Average(order => order.TotalAmount),
                LastOrderAtUtc = group.Max(order => (DateTime?)order.PlacedAtUtc),
                LastOrderValue = group
                    .OrderByDescending(order => order.PlacedAtUtc)
                    .Select(order => (decimal?)order.TotalAmount)
                    .FirstOrDefault() ?? 0m,
                RefundedOrdersCount = group.Count(order =>
                    order.Status == Domain.Modules.Orders.Enums.OrderStatus.Refunded ||
                    order.PaymentStatus == PaymentStatus.Refunded ||
                    order.PaymentStatus == PaymentStatus.PartiallyRefunded)
            })
            .ToDictionaryAsync(item => item.UserId, cancellationToken);

        var favoritesCountByUser = await _context.CustomerFavorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId.HasValue && customerIds.Contains(favorite.UserId.Value))
            .GroupBy(favorite => favorite.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.UserId!.Value, item => item.Count, cancellationToken);

        var items = customers.Select(customer =>
        {
            primaryAddressByUser.TryGetValue(customer.Id, out var address);
            orderStats.TryGetValue(customer.Id, out var stats);
            favoritesCountByUser.TryGetValue(customer.Id, out var favoritesCount);

            return new AdminCustomerListItemDto(
                customer.Id,
                customer.FullName,
                customer.Email,
                customer.Phone,
                address?.City,
                address?.Area,
                customer.AccountStatus.ToString(),
                customer.IsLoginLocked,
                customer.EmailConfirmed,
                customer.PhoneConfirmed,
                customer.CreatedAtUtc,
                customer.LastLoginAtUtc,
                customer.LastSeenAtUtc ?? customer.LastLoginAtUtc ?? stats?.LastOrderAtUtc ?? _customerPresenceService.GetLastActivityAtUtc(customer.Id),
                _customerPresenceService.IsOnline(customer.Id),
                stats?.TotalOrders ?? 0,
                stats?.TotalSpent ?? 0m,
                stats?.AverageBasket ?? 0m,
                stats?.LastOrderAtUtc,
                stats?.LastOrderValue ?? 0m,
                stats?.RefundedOrdersCount ?? 0,
                favoritesCount);
        }).ToList();

        return new PaginatedList<AdminCustomerListItemDto>(items, totalCount, page, pageSize);
    }
}
