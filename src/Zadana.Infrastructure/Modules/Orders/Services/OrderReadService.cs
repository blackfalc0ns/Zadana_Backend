using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Orders.Services;

public class OrderReadService : IOrderReadService
{
    private readonly ApplicationDbContext _dbContext;

    public OrderReadService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<OrderDto?> GetByIdAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Id == orderId && order.UserId == userId)
            .Select(order => new OrderDto(
                order.Id,
                order.OrderNumber,
                order.UserId,
                order.VendorId,
                order.CustomerAddressId,
                order.Status.ToString(),
                order.PaymentMethod.ToString(),
                order.PaymentStatus.ToString(),
                order.Subtotal,
                order.DeliveryFee,
                order.TotalAmount,
                order.PlacedAtUtc,
                order.Items.Select(item => new OrderItemDto(
                    item.Id,
                    item.VendorProductId,
                    item.MasterProductId,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice,
                    item.LineTotal)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PaginatedList<AdminVendorOrderListItemDto>> GetVendorOrdersAsync(
        Guid vendorId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.VendorId == vendorId)
            .OrderByDescending(order => order.PlacedAtUtc)
            .Select(order => new AdminVendorOrderListItemDto(
                order.Id,
                order.OrderNumber,
                order.VendorId,
                order.UserId,
                order.User.FullName,
                order.Status.ToString(),
                order.PaymentStatus.ToString(),
                order.Subtotal,
                order.DeliveryFee,
                order.CommissionAmount,
                order.TotalAmount,
                order.Items.Count,
                order.PlacedAtUtc));

        return PaginatedList<AdminVendorOrderListItemDto>.CreateAsync(query, page, pageSize, cancellationToken);
    }
}
