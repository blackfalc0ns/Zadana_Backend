using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Queries.AdminCustomers;

public record GetAdminCustomerDetailQuery(Guid CustomerId) : IRequest<AdminCustomerDetailDto>;

public class GetAdminCustomerDetailQueryHandler : IRequestHandler<GetAdminCustomerDetailQuery, AdminCustomerDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICustomerPresenceService _customerPresenceService;

    public GetAdminCustomerDetailQueryHandler(IApplicationDbContext context, ICustomerPresenceService customerPresenceService)
    {
        _context = context;
        _customerPresenceService = customerPresenceService;
    }

    public async Task<AdminCustomerDetailDto> Handle(GetAdminCustomerDetailQuery request, CancellationToken cancellationToken)
    {
        var customer = await _context.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Customer && user.Id == request.CustomerId)
            .Select(user => new
            {
                user.Id,
                user.FullName,
                user.Email,
                Phone = user.PhoneNumber,
                user.ProfilePhotoUrl,
                user.AccountStatus,
                user.IsLoginLocked,
                user.EmailConfirmed,
                PhoneConfirmed = user.PhoneNumberConfirmed,
                user.CreatedAtUtc,
                user.LastLoginAtUtc,
                user.LastSeenAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var address = await _context.CustomerAddresses
            .AsNoTracking()
            .Where(item => item.UserId == request.CustomerId)
            .OrderByDescending(item => item.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        var orderSummary = await _context.Orders
            .AsNoTracking()
            .Where(order => order.UserId == request.CustomerId)
            .GroupBy(order => order.UserId)
            .Select(group => new
            {
                TotalOrders = group.Count(),
                TotalSpent = group.Sum(order => order.TotalAmount),
                AverageBasket = group.Average(order => order.TotalAmount),
                LastOrderAtUtc = group.Max(order => (DateTime?)order.PlacedAtUtc),
                LastOrderValue = group
                    .OrderByDescending(order => order.PlacedAtUtc)
                    .Select(order => (decimal?)order.TotalAmount)
                    .FirstOrDefault() ?? 0m,
                RefundedOrdersCount = group.Count(order =>
                    order.Status == OrderStatus.Refunded ||
                    order.PaymentStatus == PaymentStatus.Refunded ||
                    order.PaymentStatus == PaymentStatus.PartiallyRefunded)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var recentOrders = await _context.Orders
            .AsNoTracking()
            .Where(order => order.UserId == request.CustomerId)
            .OrderByDescending(order => order.PlacedAtUtc)
            .Take(5)
            .Select(order => new AdminCustomerRecentOrderDto(
                order.Id,
                order.OrderNumber,
                order.PlacedAtUtc,
                order.TotalAmount,
                order.Status.ToString(),
                order.PaymentStatus.ToString()))
            .ToListAsync(cancellationToken);

        var favoritesCount = await _context.CustomerFavorites
            .AsNoTracking()
            .CountAsync(favorite => favorite.UserId == request.CustomerId, cancellationToken);

        return new AdminCustomerDetailDto(
            customer.Id,
            customer.FullName,
            customer.Email,
            customer.Phone,
            customer.ProfilePhotoUrl,
            address?.City,
            address?.Area,
            address?.AddressLine,
            address?.BuildingNo,
            address?.FloorNo,
            address?.ApartmentNo,
            address?.Label?.ToString(),
            customer.AccountStatus.ToString(),
            customer.IsLoginLocked,
            customer.EmailConfirmed,
            customer.PhoneConfirmed,
            customer.CreatedAtUtc,
            customer.LastLoginAtUtc,
            customer.LastSeenAtUtc ?? customer.LastLoginAtUtc ?? orderSummary?.LastOrderAtUtc ?? _customerPresenceService.GetLastActivityAtUtc(customer.Id),
            _customerPresenceService.IsOnline(customer.Id),
            orderSummary?.TotalOrders ?? 0,
            orderSummary?.TotalSpent ?? 0m,
            orderSummary?.AverageBasket ?? 0m,
            orderSummary?.LastOrderAtUtc,
            orderSummary?.LastOrderValue ?? 0m,
            orderSummary?.RefundedOrdersCount ?? 0,
            favoritesCount,
            recentOrders);
    }
}
