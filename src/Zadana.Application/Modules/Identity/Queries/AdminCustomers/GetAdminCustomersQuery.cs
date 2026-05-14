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
    string? Status = null,
    string? City = null,
    bool? IsLocked = null,
    bool? HasOrders = null,
    decimal? MinSpent = null,
    decimal? MaxSpent = null,
    string? SortBy = null,
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

        // Filter by account status
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (Enum.TryParse<AccountStatus>(request.Status, ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(user => user.AccountStatus == parsedStatus);
            }
        }

        // Filter by login locked
        if (request.IsLocked.HasValue)
        {
            query = query.Where(user => user.IsLoginLocked == request.IsLocked.Value);
        }

        // Filter by city (via address join)
        if (!string.IsNullOrWhiteSpace(request.City))
        {
            var city = request.City.Trim();
            var userIdsInCity = _context.CustomerAddresses
                .AsNoTracking()
                .Where(address => address.City != null && address.City.Contains(city))
                .Select(address => address.UserId);

            query = query.Where(user => userIdsInCity.Contains(user.Id));
        }

        // Filter by hasOrders / spending — requires joining order stats
        // We collect IDs first for spend/order filters that need aggregation
        var needsOrderFilter = request.HasOrders.HasValue || request.MinSpent.HasValue || request.MaxSpent.HasValue;
        if (needsOrderFilter)
        {
            var orderStatsFilter = _context.Orders
                .AsNoTracking()
                .GroupBy(order => order.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    TotalOrders = group.Count(),
                    TotalSpent = group.Sum(order => order.TotalAmount)
                });

            if (request.HasOrders == true)
            {
                var usersWithOrders = orderStatsFilter
                    .Where(stats => stats.TotalOrders > 0)
                    .Select(stats => stats.UserId);
                query = query.Where(user => usersWithOrders.Contains(user.Id));
            }
            else if (request.HasOrders == false)
            {
                var usersWithOrders = orderStatsFilter
                    .Where(stats => stats.TotalOrders > 0)
                    .Select(stats => stats.UserId);
                query = query.Where(user => !usersWithOrders.Contains(user.Id));
            }

            if (request.MinSpent.HasValue)
            {
                var usersAboveMin = orderStatsFilter
                    .Where(stats => stats.TotalSpent >= request.MinSpent.Value)
                    .Select(stats => stats.UserId);
                query = query.Where(user => usersAboveMin.Contains(user.Id));
            }

            if (request.MaxSpent.HasValue)
            {
                var usersBelowMax = orderStatsFilter
                    .Where(stats => stats.TotalSpent <= request.MaxSpent.Value)
                    .Select(stats => stats.UserId);
                query = query.Where(user => usersBelowMax.Contains(user.Id));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Sorting
        var sortBy = request.SortBy?.Trim().ToLowerInvariant();
        query = sortBy switch
        {
            "name" => query.OrderBy(user => user.FullName),
            "name_desc" => query.OrderByDescending(user => user.FullName),
            "created" => query.OrderBy(user => user.CreatedAtUtc),
            "last_login" => query.OrderByDescending(user => user.LastLoginAtUtc),
            _ => query.OrderByDescending(user => user.CreatedAtUtc)
        };

        var customers = await query
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
