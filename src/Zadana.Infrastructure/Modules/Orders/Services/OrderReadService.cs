using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
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

    public async Task<CustomerOrderTrackingDto?> GetCustomerOrderTrackingAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(x => x.StatusHistory)
            .Where(x => x.Id == orderId && x.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        var assignment = await _dbContext.DeliveryAssignments
            .AsNoTracking()
            .Include(x => x.Driver)
            .ThenInclude(x => x!.User)
            .Where(x => x.OrderId == order.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var timeline = BuildTimeline(order);
        var estimatedDelivery = BuildEstimatedDelivery(order, assignment);
        var driver = BuildDriver(assignment);

        return new CustomerOrderTrackingDto(
            new CustomerOrderTrackingOrderDto(order.Id, MapTrackingStatus(order.Status)),
            estimatedDelivery,
            driver,
            timeline);
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

    private static List<CustomerOrderTrackingTimelineItemDto> BuildTimeline(Order order)
    {
        var history = order.StatusHistory
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();

        var isCancelled = order.Status is OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed;
        var isReturning = order.Status == OrderStatus.Refunded;
        var terminalId = isCancelled ? "cancelled" : isReturning ? "returning" : "delivered";
        var terminalTitle = isCancelled ? "Order cancelled" : isReturning ? "Return in progress" : "Delivered";

        var steps = new List<TrackingStepDefinition>
        {
            new("order_placed", "Order placed", GetStepTime(order.PlacedAtUtc), IsCurrentStage(order.Status, TrackingStage.OrderPlaced), IsCompletedStage(order.Status, TrackingStage.OrderPlaced)),
            new("vendor_confirmed", "Vendor confirmed", GetStepTime(ResolveStepDate(history, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.DriverAssignmentInProgress, OrderStatus.DriverAssigned, OrderStatus.PickedUp, OrderStatus.OnTheWay, OrderStatus.Delivered, OrderStatus.Refunded)), IsCurrentStage(order.Status, TrackingStage.VendorConfirmed), IsCompletedStage(order.Status, TrackingStage.VendorConfirmed)),
            new("preparing", "Preparing order", GetStepTime(ResolveStepDate(history, OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.DriverAssignmentInProgress, OrderStatus.DriverAssigned, OrderStatus.PickedUp, OrderStatus.OnTheWay, OrderStatus.Delivered, OrderStatus.Refunded)), IsCurrentStage(order.Status, TrackingStage.Preparing), IsCompletedStage(order.Status, TrackingStage.Preparing)),
            new("out_for_delivery", "Out for delivery", GetStepTime(ResolveStepDate(history, OrderStatus.PickedUp, OrderStatus.OnTheWay, OrderStatus.Delivered, OrderStatus.Refunded)), IsCurrentStage(order.Status, TrackingStage.OutForDelivery), IsCompletedStage(order.Status, TrackingStage.OutForDelivery))
        };

        var terminalTime = terminalId switch
        {
            "cancelled" => GetStepTime(order.CancelledAtUtc ?? ResolveStepDate(history, OrderStatus.Cancelled, OrderStatus.VendorRejected, OrderStatus.DeliveryFailed)),
            "returning" => GetStepTime(ResolveStepDate(history, OrderStatus.Refunded)),
            _ => GetStepTime(order.DeliveredAtUtc ?? ResolveStepDate(history, OrderStatus.Delivered))
        };

        steps.Add(new TrackingStepDefinition(
            terminalId,
            terminalTitle,
            terminalTime,
            IsTerminalActive(order.Status),
            IsTerminalCompleted(order.Status)));

        return steps
            .Select(step => new CustomerOrderTrackingTimelineItemDto(
                step.Id,
                step.Title,
                step.Time,
                step.IsActive,
                step.IsCompleted))
            .ToList();
    }

    private static CustomerOrderEstimatedDeliveryDto? BuildEstimatedDelivery(Order order, DeliveryAssignment? assignment)
    {
        if (order.Status is OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded)
        {
            return null;
        }

        var estimatedAtUtc = order.Status switch
        {
            OrderStatus.Delivered => order.DeliveredAtUtc ?? ResolveHistoryDate(order, OrderStatus.Delivered) ?? order.PlacedAtUtc,
            OrderStatus.OnTheWay => (assignment?.PickedUpAtUtc ?? assignment?.AcceptedAtUtc ?? order.PlacedAtUtc).AddMinutes(30),
            OrderStatus.PickedUp => (assignment?.PickedUpAtUtc ?? order.PlacedAtUtc).AddMinutes(30),
            OrderStatus.DriverAssigned => (assignment?.AcceptedAtUtc ?? order.PlacedAtUtc).AddMinutes(45),
            OrderStatus.DriverAssignmentInProgress => order.PlacedAtUtc.AddMinutes(45),
            _ => order.PlacedAtUtc.AddMinutes(45)
        };

        return new CustomerOrderEstimatedDeliveryDto(
            estimatedAtUtc,
            estimatedAtUtc.ToString("dd MMM yyyy, hh:mm tt 'UTC'", CultureInfo.InvariantCulture));
    }

    private static CustomerOrderTrackingDriverDto? BuildDriver(DeliveryAssignment? assignment)
    {
        if (assignment?.Driver?.User is null)
        {
            return null;
        }

        return new CustomerOrderTrackingDriverDto(
            assignment.Driver.Id,
            assignment.Driver.User.FullName,
            assignment.Driver.User.PhoneNumber,
            string.IsNullOrWhiteSpace(assignment.Driver.VehicleType) ? "Delivery Driver" : assignment.Driver.VehicleType.Trim());
    }

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

    private static string MapTrackingStatus(OrderStatus status) =>
        status switch
        {
            OrderStatus.PendingPayment or OrderStatus.Placed => "pending",
            OrderStatus.PendingVendorAcceptance or OrderStatus.Accepted or OrderStatus.Preparing or
            OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or
            OrderStatus.DriverAssigned or OrderStatus.PickedUp => "processing",
            OrderStatus.OnTheWay => "out_for_delivery",
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

    private static bool IsCurrentStage(OrderStatus status, TrackingStage stage) =>
        stage switch
        {
            TrackingStage.OrderPlaced => status is OrderStatus.PendingPayment or OrderStatus.Placed,
            TrackingStage.VendorConfirmed => status is OrderStatus.PendingVendorAcceptance or OrderStatus.Accepted,
            TrackingStage.Preparing => status is OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned,
            TrackingStage.OutForDelivery => status is OrderStatus.PickedUp or OrderStatus.OnTheWay,
            _ => false
        };

    private static bool IsCompletedStage(OrderStatus status, TrackingStage stage) =>
        stage switch
        {
            TrackingStage.OrderPlaced => status != OrderStatus.PendingPayment,
            TrackingStage.VendorConfirmed => status is OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned or OrderStatus.PickedUp or OrderStatus.OnTheWay or OrderStatus.Delivered or OrderStatus.Refunded or OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed,
            TrackingStage.Preparing => status is OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned or OrderStatus.PickedUp or OrderStatus.OnTheWay or OrderStatus.Delivered or OrderStatus.Refunded,
            TrackingStage.OutForDelivery => status is OrderStatus.OnTheWay or OrderStatus.Delivered or OrderStatus.Refunded,
            _ => false
        };

    private static bool IsTerminalActive(OrderStatus status) =>
        status is OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.VendorRejected or OrderStatus.DeliveryFailed or OrderStatus.Refunded;

    private static bool IsTerminalCompleted(OrderStatus status) =>
        status == OrderStatus.Delivered;

    private static DateTime? ResolveStepDate(IReadOnlyCollection<OrderStatusHistory> history, params OrderStatus[] statuses) =>
        history
            .Where(x => statuses.Contains(x.NewStatus))
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => (DateTime?)x.CreatedAtUtc)
            .FirstOrDefault();

    private static DateTime? ResolveHistoryDate(Order order, params OrderStatus[] statuses) =>
        ResolveStepDate(order.StatusHistory.ToList(), statuses);

    private static string GetStepTime(DateTime? dateTimeUtc) =>
        dateTimeUtc.HasValue
            ? dateTimeUtc.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture)
            : string.Empty;

    private sealed record TrackingStepDefinition(
        string Id,
        string Title,
        string Time,
        bool IsActive,
        bool IsCompleted);

    private enum TrackingStage
    {
        OrderPlaced,
        VendorConfirmed,
        Preparing,
        OutForDelivery
    }
}
