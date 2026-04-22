using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Orders.Controllers;

[Route("api/admin/orders")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin Dashboard API")]
public class AdminOrdersController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOrderReadService _orderReadService;
    private readonly IPublisher _publisher;
    private readonly IOrderStatusNotificationDispatcher _orderStatusNotificationDispatcher;

    public AdminOrdersController(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IOrderReadService orderReadService,
        IPublisher publisher,
        IOrderStatusNotificationDispatcher orderStatusNotificationDispatcher)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _orderReadService = orderReadService;
        _publisher = publisher;
        _orderStatusNotificationDispatcher = orderStatusNotificationDispatcher;
    }

    [HttpGet]
    public async Task<ActionResult<AdminOrdersListDto>> GetOrders(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? paymentStatus,
        [FromQuery] string? fulfillmentStatus,
        [FromQuery] string? queueView,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _orderReadService.GetAdminOrdersAsync(
            search,
            status,
            paymentStatus,
            fulfillmentStatus,
            queueView,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<AdminOrderDetailDto>> GetOrderById(Guid orderId, CancellationToken cancellationToken = default)
    {
        var result = await _orderReadService.GetAdminOrderDetailAsync(orderId, cancellationToken);
        if (result is null)
        {
            throw new NotFoundException("Order", orderId);
        }

        return Ok(result);
    }

    [HttpPost("{orderId:guid}/status")]
    public async Task<ActionResult<AdminOrderDetailDto>> UpdateStatus(
        Guid orderId,
        [FromBody] AdminOrderStatusUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderWithUserAsync(orderId, cancellationToken);
        var adminUserId = GetRequiredAdminUserId();
        var oldStatus = order.Status;

        var newStatus = request.NewStatus?.Trim().ToUpperInvariant() switch
        {
            "NEW" => OrderStatus.PendingVendorAcceptance,
            "PENDING" => OrderStatus.Accepted,
            "IN_PROGRESS" => OrderStatus.Preparing,
            "OUT_FOR_DELIVERY" => OrderStatus.OnTheWay,
            "DELIVERED" => OrderStatus.Delivered,
            "COMPLETED" => OrderStatus.Refunded,
            "CANCELLED" => OrderStatus.Cancelled,
            _ => throw new BusinessRuleException("INVALID_STATUS", "Invalid admin order status.")
        };

        order.ChangeStatus(newStatus, adminUserId, request.AdminNotes);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Dispatch customer push notification
        await _orderStatusNotificationDispatcher.DispatchCustomerAsync(
            new OrderStatusCustomerNotificationRequest(
                order.UserId,
                order.Id,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                newStatus,
                ActorRole: "admin"),
            cancellationToken);

        // Publish event for vendor notification and other handlers
        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                newStatus,
                NotifyCustomer: true,
                NotifyVendor: true,
                ActorRole: "admin",
                CustomerNotificationAlreadySent: true),
            cancellationToken);

        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/assign-driver")]
    public async Task<ActionResult<AdminOrderDetailDto>> AssignDriver(
        Guid orderId,
        [FromBody] AdminAssignDriverRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderWithUserAsync(orderId, cancellationToken);
        var oldStatus = order.Status;
        var driverId = ParseGuid(request.SelectedDriverId, "driver");
        var driver = await _dbContext.Drivers.FirstOrDefaultAsync(item => item.Id == driverId, cancellationToken)
            ?? throw new NotFoundException("Driver", driverId);

        var assignment = await _dbContext.DeliveryAssignments
            .FirstOrDefaultAsync(item => item.OrderId == order.Id, cancellationToken);

        if (assignment is null)
        {
            assignment = new DeliveryAssignment(order.Id, order.PaymentMethod == PaymentMethodType.CashOnDelivery ? order.TotalAmount : 0);
            _dbContext.DeliveryAssignments.Add(assignment);
        }

        assignment.OfferTo(driver.Id);
        assignment.Accept();
        order.ChangeStatus(OrderStatus.DriverAssigned, GetRequiredAdminUserId(), request.InternalNotes ?? "Driver assigned by admin.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Dispatch customer push notification
        await _orderStatusNotificationDispatcher.DispatchCustomerAsync(
            new OrderStatusCustomerNotificationRequest(
                order.UserId,
                order.Id,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                OrderStatus.DriverAssigned,
                ActorRole: "admin"),
            cancellationToken);

        // Publish event for vendor notification and other handlers
        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                OrderStatus.DriverAssigned,
                NotifyCustomer: true,
                NotifyVendor: true,
                ActorRole: "admin",
                CustomerNotificationAlreadySent: true),
            cancellationToken);

        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/cancel")]
    public async Task<ActionResult<AdminOrderDetailDto>> CancelOrder(
        Guid orderId,
        [FromBody] AdminCancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderWithUserAsync(orderId, cancellationToken);
        var oldStatus = order.Status;
        order.ChangeStatus(OrderStatus.Cancelled, GetRequiredAdminUserId(), request.InternalNote ?? request.Details ?? "Cancelled by admin.");

        if (request.RefundType is "full" or "partial")
        {
            await EnsureRefundAsync(order, request.RefundType == "full" ? order.TotalAmount : order.TotalAmount / 2m, request.Details, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Dispatch customer push notification
        await _orderStatusNotificationDispatcher.DispatchCustomerAsync(
            new OrderStatusCustomerNotificationRequest(
                order.UserId,
                order.Id,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                OrderStatus.Cancelled,
                ActorRole: "admin"),
            cancellationToken);

        // Publish event for vendor notification and other handlers
        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                OrderStatus.Cancelled,
                NotifyCustomer: true,
                NotifyVendor: true,
                ActorRole: "admin",
                CustomerNotificationAlreadySent: true),
            cancellationToken);
        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/refund")]
    public async Task<ActionResult<AdminOrderDetailDto>> CreateRefund(
        Guid orderId,
        [FromBody] AdminRefundOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderId, cancellationToken);
        var amount = decimal.TryParse(request.RefundAmount, out var parsed) && parsed > 0
            ? parsed
            : order.TotalAmount;

        await EnsureRefundAsync(order, amount, request.InternalNotes ?? request.Reason, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/dispute")]
    public async Task<ActionResult<AdminOrderDetailDto>> OpenDispute(
        Guid orderId,
        [FromBody] AdminDisputeOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderId, cancellationToken);
        var complaint = new OrderComplaint(order.Id, $"Dispute: {request.DisputeType}. {request.Description}".Trim());
        complaint.MarkInReview();
        _dbContext.OrderComplaints.Add(complaint);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/issue-flag")]
    public async Task<ActionResult<AdminOrderDetailDto>> FlagIssue(
        Guid orderId,
        [FromBody] AdminIssueFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderId, cancellationToken);
        var complaint = new OrderComplaint(order.Id, $"Issue: {request.IssueType}. {request.RequiredAction}".Trim());
        complaint.MarkInReview();
        _dbContext.OrderComplaints.Add(complaint);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/resolve-operational-case")]
    public async Task<ActionResult<AdminOrderDetailDto>> ResolveOperationalCase(Guid orderId, CancellationToken cancellationToken = default)
    {
        var complaint = await _dbContext.OrderComplaints
            .Where(item => item.OrderId == orderId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (complaint is null)
        {
            throw new NotFoundException("OperationalCase", orderId);
        }

        complaint.Resolve();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/close-operational-case")]
    public async Task<ActionResult<AdminOrderDetailDto>> CloseOperationalCase(Guid orderId, CancellationToken cancellationToken = default)
    {
        var complaint = await _dbContext.OrderComplaints
            .Where(item => item.OrderId == orderId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (complaint is null)
        {
            throw new NotFoundException("OperationalCase", orderId);
        }

        complaint.Resolve();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/reopen-operational-case")]
    public async Task<ActionResult<AdminOrderDetailDto>> ReopenOperationalCase(Guid orderId, CancellationToken cancellationToken = default)
    {
        var complaint = await _dbContext.OrderComplaints
            .Where(item => item.OrderId == orderId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (complaint is null)
        {
            throw new NotFoundException("OperationalCase", orderId);
        }

        complaint.MarkInReview();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    private async Task<Order> LoadOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await _dbContext.Orders.FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);
    }

    private Task<Order> LoadOrderWithUserAsync(Guid orderId, CancellationToken cancellationToken) =>
        LoadOrderAsync(orderId, cancellationToken);

    private async Task<AdminOrderDetailDto> RequireDetailAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await _orderReadService.GetAdminOrderDetailAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);
    }

    private Guid GetRequiredAdminUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
    }

    private static Guid ParseGuid(string? value, string entityName)
    {
        if (!Guid.TryParse(value, out var parsed))
        {
            throw new BusinessRuleException("INVALID_ID", $"Invalid {entityName} id.");
        }

        return parsed;
    }

    private async Task EnsureRefundAsync(Order order, decimal amount, string? reason, CancellationToken cancellationToken)
    {
        var payment = await _dbContext.Payments
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(item => item.OrderId == order.Id, cancellationToken);

        if (payment is null)
        {
            payment = new Payment(order.Id, order.PaymentMethod, order.TotalAmount);
            payment.MarkAsPaid();
            _dbContext.Payments.Add(payment);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var refund = new Refund(payment.Id, Math.Min(amount, order.TotalAmount), reason);
        refund.Process();
        _dbContext.Refunds.Add(refund);
        order.UpdatePaymentStatus(PaymentStatus.Refunded);
    }
}

public record AdminOrderStatusUpdateRequest(
    string? NewStatus,
    string? AdminNotes,
    string? ExpectedDeliveryTime,
    bool NotifyCustomer,
    bool NotifyMerchant,
    bool NotifyDriver,
    bool AddInternalLog);

public record AdminAssignDriverRequest(
    string? SearchQuery,
    string? City,
    string? Availability,
    string? Verification,
    string? SelectedDriverId,
    string? AssignmentReason,
    string? InternalNotes,
    bool NotifyDriver,
    bool NotifyMerchant,
    bool NotifyCustomer);

public record AdminCancelOrderRequest(
    string? Reason,
    string? Details,
    string? RefundType,
    string? CostBearer,
    bool NotifyCustomer,
    bool NotifyMerchant,
    bool NotifyDriver,
    string? CustomerMessage,
    string? InternalNote);

public record AdminRefundOrderRequest(
    string? RefundType,
    string? RefundAmount,
    string? Reason,
    string? RefundMethod,
    string? CostBearer,
    string? InternalNotes,
    string? CustomerMessage,
    bool NotifyCustomerSms,
    bool NotifyFinance);

public record AdminDisputeOrderRequest(
    string? DisputeType,
    string? Priority,
    string? RouteTo,
    string? Description,
    string? InternalNotes,
    bool NotifyReviewer,
    bool AddToLog,
    bool MarkHighRisk,
    bool NotifyStakeholders);

public record AdminIssueFlagRequest(
    string? IssueType,
    string? Priority,
    string? RequiredAction,
    string? AssignedTeam,
    string? FollowUpDate,
    bool ShowInOperationsCenter,
    bool NotifyAssignedTeam,
    bool HighRiskAlert);
