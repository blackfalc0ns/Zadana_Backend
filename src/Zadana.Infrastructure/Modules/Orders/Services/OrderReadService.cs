using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
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

    public async Task<CustomerOrderListDto> GetCustomerOrdersAsync(
        Guid userId,
        CustomerOrderBucket bucket,
        int page,
        int perPage,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPerPage = perPage <= 0 ? 20 : perPage;

        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.UserId == userId);

        query = bucket switch
        {
            CustomerOrderBucket.Completed => query.Where(order => order.Status == OrderStatus.Delivered),
            CustomerOrderBucket.Returns => query.Where(order => order.Status == OrderStatus.Refunded),
            _ => query.Where(order =>
                order.Status != OrderStatus.Delivered &&
                order.Status != OrderStatus.Refunded &&
                order.Status != OrderStatus.Cancelled &&
                order.Status != OrderStatus.VendorRejected &&
                order.Status != OrderStatus.DeliveryFailed)
        };

        var total = await query.CountAsync(cancellationToken);
        var orders = await query
            .Include(order => order.Items)
            .OrderByDescending(order => order.PlacedAtUtc)
            .Skip((normalizedPage - 1) * normalizedPerPage)
            .Take(normalizedPerPage)
            .ToListAsync(cancellationToken);

        var items = orders.Select(MapListItem).ToList();

        return new CustomerOrderListDto(items, normalizedPage, normalizedPerPage, total);
    }

    public async Task<CustomerOrderDetailDto?> GetCustomerOrderDetailAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.Id == orderId && order.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return order is null ? null : MapDetail(order);
    }

    public async Task<OrderComplaintDto?> GetCustomerOrderComplaintAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var complaint = await _dbContext.OrderComplaints
            .AsNoTracking()
            .Include(complaint => complaint.Attachments)
            .Where(complaint => complaint.OrderId == orderId && complaint.Order.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
        return complaint is null ? null : MapComplaint(complaint);
    }

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

    private static CustomerOrderListItemDto MapListItem(Order order) =>
        new(
            order.Id,
            order.PlacedAtUtc,
            order.TotalAmount,
            MapStatus(order.Status),
            order.Items.Count,
            order.Items
                .Select(item => new CustomerOrderProductDto(
                    item.Id,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice))
                .ToList());

    private static CustomerOrderDetailDto MapDetail(Order order) =>
        new(
            order.Id,
            order.PlacedAtUtc,
            order.TotalAmount,
            MapStatus(order.Status),
            CanCancel(order.Status),
            order.Items.Count,
            new CustomerOrderPriceSummaryDto(
                order.Subtotal,
                order.DeliveryFee,
                order.TotalAmount),
            order.Items
                .Select(item => new CustomerOrderProductDto(
                    item.Id,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice))
                .ToList());

    private static OrderComplaintDto MapComplaint(OrderComplaint complaint) =>
        new(
            complaint.Id,
            MapComplaintStatus(complaint.Status),
            complaint.Message,
            complaint.Attachments
                .Select(attachment => new OrderComplaintAttachmentDto(
                    attachment.FileName,
                    attachment.FileUrl))
                .ToList(),
            complaint.CreatedAtUtc);

    private static string MapStatus(OrderStatus status) =>
        status switch
        {
            OrderStatus.PendingPayment or OrderStatus.Placed or OrderStatus.PendingVendorAcceptance => "pending",
            OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.ReadyForPickup or
            OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned or
            OrderStatus.PickedUp or OrderStatus.OnTheWay => "processing",
            OrderStatus.Delivered => "delivered",
            OrderStatus.Refunded => "returning",
            _ => "cancelled"
        };

    private static bool CanCancel(OrderStatus status) =>
        status is OrderStatus.PendingPayment or
            OrderStatus.Placed or
            OrderStatus.PendingVendorAcceptance or
            OrderStatus.Accepted or
            OrderStatus.Preparing or
            OrderStatus.ReadyForPickup or
            OrderStatus.DriverAssignmentInProgress;

    private static string MapComplaintStatus(OrderComplaintStatus status) =>
        status switch
        {
            OrderComplaintStatus.Submitted => "submitted",
            OrderComplaintStatus.InReview => "in_review",
            OrderComplaintStatus.Resolved => "resolved",
            _ => "submitted"
        };
}
