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
using Zadana.Application.Modules.Delivery.Support;
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
    private static readonly TimeSpan PickupOtpTtl = TimeSpan.FromHours(12);

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOrderReadService _orderReadService;
    private readonly IOrderSupportCaseWorkflowService _orderSupportCaseWorkflowService;
    private readonly IPublisher _publisher;
    private readonly IOrderStatusNotificationDispatcher _orderStatusNotificationDispatcher;
    private readonly Application.Modules.Delivery.Interfaces.IDeliveryDispatchService _dispatchService;
    private readonly INotificationService _notificationService;

    public AdminOrdersController(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IOrderReadService orderReadService,
        IOrderSupportCaseWorkflowService orderSupportCaseWorkflowService,
        IPublisher publisher,
        IOrderStatusNotificationDispatcher orderStatusNotificationDispatcher,
        Application.Modules.Delivery.Interfaces.IDeliveryDispatchService dispatchService,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _orderReadService = orderReadService;
        _orderSupportCaseWorkflowService = orderSupportCaseWorkflowService;
        _publisher = publisher;
        _orderStatusNotificationDispatcher = orderStatusNotificationDispatcher;
        _dispatchService = dispatchService;
        _notificationService = notificationService;
    }

    [HttpGet("filter-options")]
    public ActionResult<AdminOrderFilterOptionsDto> GetFilterOptions()
    {
        return Ok(AdminOrderFilterOptionsFactory.Build());
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

        // Admin can only set pre-delivery statuses.
        // Delivery lifecycle (OUT_FOR_DELIVERY, DELIVERED) is driven by driver actions.
        // Cancellation has its own dedicated endpoint.
        var newStatus = request.NewStatus?.Trim().ToUpperInvariant() switch
        {
            "NEW" => OrderStatus.PendingVendorAcceptance,
            "PENDING" => OrderStatus.Accepted,
            "IN_PROGRESS" => OrderStatus.Preparing,
            _ => throw new BusinessRuleException("INVALID_STATUS",
                "Admin can only set status to NEW, PENDING, or IN_PROGRESS. Use dedicated endpoints for delivery, completion, and cancellation.")
        };

        order.ChangeStatus(newStatus, adminUserId, request.AdminNotes);
        _dbContext.OrderStatusHistories.Add(order.StatusHistory.Last());
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

        if (!driver.CanReceiveNewOffers)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "Driver must be approved, online, unrestricted, and ready to receive new offers before assignment.");
        }

        if (!await DriverMatchesDeliveryAreaAsync(driver, order, cancellationToken))
        {
            throw new BusinessRuleException(
                "DRIVER_CITY_MISMATCH",
                "Driver cannot be assigned because their city does not match the store and customer city.");
        }

        var assignment = await _dbContext.DeliveryAssignments
            .FirstOrDefaultAsync(item => item.OrderId == order.Id, cancellationToken);

        var previousDriverId = assignment?.DriverId;

        if (assignment is null)
        {
            assignment = new DeliveryAssignment(order.Id, order.PaymentMethod == PaymentMethodType.CashOnDelivery ? order.TotalAmount : 0);
            _dbContext.DeliveryAssignments.Add(assignment);
        }
        else
        {
            assignment.UpdateCodAmount(order.PaymentMethod == PaymentMethodType.CashOnDelivery ? order.TotalAmount : 0);
        }

        assignment.OfferTo(driver.Id, assignment.DispatchAttemptNumber + 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();
        EnsurePickupOtpForAdminAssignment(assignment, previousDriverId, driver.Id);
        order.ChangeStatus(OrderStatus.DriverAssigned, GetRequiredAdminUserId(), request.InternalNotes ?? "Driver assigned by admin.");
        _dbContext.OrderStatusHistories.Add(order.StatusHistory.Last());

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

    [HttpPost("{orderId:guid}/dispatch/recompute")]
    public async Task<ActionResult<AdminOrderDetailDto>> RecomputeDispatch(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderId, cancellationToken);

        if (order.Status is not (OrderStatus.ReadyForPickup or OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned))
        {
            throw new BusinessRuleException(
                "INVALID_DISPATCH_STATE",
                "Dispatch can only be recomputed for orders waiting for pickup or driver assignment.");
        }

        if (order.Status == OrderStatus.DriverAssigned)
        {
            var oldStatus = order.Status;
            order.ChangeStatus(
                OrderStatus.DriverAssignmentInProgress,
                GetRequiredAdminUserId(),
                "Dispatch recompute requested by admin.");
            _dbContext.OrderStatusHistories.Add(order.StatusHistory.Last());
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _publisher.Publish(
                new OrderStatusChangedNotification(
                    order.Id,
                    order.UserId,
                    order.VendorId,
                    order.OrderNumber,
                    oldStatus,
                    order.Status,
                    NotifyCustomer: true,
                    NotifyVendor: true,
                    ActorRole: "admin"),
                cancellationToken);
        }

        await _dispatchService.TryAutoDispatchAsync(orderId, resetCycle: true, cancellationToken: cancellationToken);
        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/cancel")]
    public async Task<ActionResult<AdminOrderDetailDto>> CancelOrder(
        Guid orderId,
        [FromBody] AdminCancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderWithUserAsync(orderId, cancellationToken);
        var adminUserId = GetRequiredAdminUserId();
        var oldStatus = order.Status;

        // Close any active delivery assignment
        var assignment = await _dbContext.DeliveryAssignments
            .Include(a => a.Driver)
            .FirstOrDefaultAsync(a => a.OrderId == orderId
                && a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Delivered
                && a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Failed
                && a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Cancelled
                && a.Status != Domain.Modules.Delivery.Enums.AssignmentStatus.Returned, cancellationToken);

        Guid? driverUserId = assignment?.Driver?.UserId;
        Guid? assignmentId = assignment?.Id;

        if (assignment is not null)
        {
            assignment.Cancel(request.InternalNote ?? request.Details ?? "Order cancelled by admin");
        }

        order.ChangeStatus(OrderStatus.Cancelled, adminUserId, request.InternalNote ?? request.Details ?? "Cancelled by admin.");
        _dbContext.OrderStatusHistories.Add(order.StatusHistory.Last());

        if (request.RefundType is "full" or "partial")
        {
            await EnsureRefundAsync(
                order,
                adminUserId,
                request.RefundType == "full" ? order.TotalAmount : order.TotalAmount / 2m,
                request.Details,
                cancellationToken);
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

        if (driverUserId.HasValue && assignmentId.HasValue)
        {
            await _notificationService.SendAssignmentUpdatedToDriverAsync(
                driverUserId.Value,
                assignmentId.Value,
                orderId,
                cancellationToken);

            await _notificationService.SendDriverHomeUpdatedAsync(
                driverUserId.Value,
                cancellationToken);
        }

        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/refund")]
    public async Task<ActionResult<AdminOrderDetailDto>> CreateRefund(
        Guid orderId,
        [FromBody] AdminRefundOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderId, cancellationToken);
        var adminUserId = GetRequiredAdminUserId();
        var amount = decimal.TryParse(request.RefundAmount, out var parsed) && parsed > 0
            ? parsed
            : order.TotalAmount;

        var activeSupportCase = await LoadActiveSupportCaseAsync(orderId, cancellationToken);
        if (activeSupportCase is not null && activeSupportCase.Type != OrderSupportCaseType.ReturnRequest)
        {
            throw new BusinessRuleException(
                "ORDER_SUPPORT_CASE_ALREADY_EXISTS",
                "An active non-return support case already exists for this order.");
        }

        activeSupportCase ??= await _orderSupportCaseWorkflowService.CreateAdminCaseAsync(
            orderId,
            adminUserId,
            "return_request",
            request.Reason,
            BuildSupportCaseMessage("Admin refund review opened.", request.Reason, request.InternalNotes),
            "high",
            "finance",
            request.InternalNotes,
            request.CustomerMessage,
            cancellationToken);

        await _orderSupportCaseWorkflowService.ApproveAsync(
            activeSupportCase.Id,
            adminUserId,
            amount,
            request.RefundMethod,
            request.CostBearer,
            request.InternalNotes ?? request.Reason,
            request.CustomerMessage,
            cancellationToken);

        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/dispute")]
    public async Task<ActionResult<AdminOrderDetailDto>> OpenDispute(
        Guid orderId,
        [FromBody] AdminDisputeOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        await LoadOrderAsync(orderId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new BusinessRuleException(
                "DISPUTE_DESCRIPTION_REQUIRED",
                "Dispute description is required.");
        }

        var adminUserId = GetRequiredAdminUserId();
        var priority = request.MarkHighRisk ? "critical" : request.Priority;
        var queue = request.MarkHighRisk ? "risk" : ResolveAdminQueue(request.RouteTo);
        var attachments = BuildSupportCaseAttachments(request.Attachments);
        var internalNote = BuildDisputeInternalNote(request);
        var activeSupportCase = await LoadActiveSupportCaseAsync(orderId, cancellationToken);

        if (activeSupportCase is not null)
        {
            await _orderSupportCaseWorkflowService.EscalateAsync(
                activeSupportCase.Id,
                adminUserId,
                queue,
                priority,
                BuildDisputeEscalationNote(request, internalNote),
                request.NotifyStakeholders ? request.Description : null,
                notifyEscalatedTeam: request.NotifyReviewer,
                notifyCurrentReviewer: request.NotifyReviewer,
                slaDueAtUtc: null,
                cancellationToken: cancellationToken,
                attachments: attachments,
                notifyStakeholders: request.NotifyStakeholders);

            return Ok(await RequireDetailAsync(orderId, cancellationToken));
        }

        await _orderSupportCaseWorkflowService.CreateAdminCaseAsync(
            orderId,
            adminUserId,
            "complaint",
            request.DisputeType,
            BuildSupportCaseMessage("Admin dispute opened.", request.Description),
            priority,
            queue,
            internalNote,
            null,
            cancellationToken,
            attachments,
            request.NotifyReviewer,
            request.NotifyStakeholders,
            initiatorRole: request.NotifyStakeholders ? "customer" : "admin");

        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/issue-flag")]
    public async Task<ActionResult<AdminOrderDetailDto>> FlagIssue(
        Guid orderId,
        [FromBody] AdminIssueFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        await LoadOrderAsync(orderId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.RequiredAction))
        {
            throw new BusinessRuleException(
                "ISSUE_REQUIRED_ACTION_REQUIRED",
                "Required action is required.");
        }

        var priority = request.HighRiskAlert ? "critical" : request.Priority;
        var queue = request.HighRiskAlert ? "risk" : ResolveIssueQueue(request.AssignedTeam);

        await _orderSupportCaseWorkflowService.CreateAdminCaseAsync(
            orderId,
            GetRequiredAdminUserId(),
            "complaint",
            request.IssueType,
            BuildSupportCaseMessage("Operational issue flagged.", request.RequiredAction),
            priority,
            queue,
            BuildIssueInternalNote(request),
            null,
            cancellationToken,
            notifyReviewer: request.NotifyAssignedTeam || request.ShowInOperationsCenter || request.HighRiskAlert,
            notifyStakeholders: false,
            initiatorRole: "admin");

        return Ok(await RequireDetailAsync(orderId, cancellationToken));
    }

    [HttpPost("{orderId:guid}/resolve-operational-case")]
    public async Task<ActionResult<AdminOrderDetailDto>> ResolveOperationalCase(Guid orderId, CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadLatestSupportCaseAsync(orderId, cancellationToken);
        if (supportCase is not null)
        {
            await _orderSupportCaseWorkflowService.ResolveAsync(
                supportCase.Id,
                GetRequiredAdminUserId(),
                "Operational case resolved by admin.",
                cancellationToken);

            return Ok(await RequireDetailAsync(orderId, cancellationToken));
        }

        var complaint = await LoadLatestLegacyComplaintAsync(orderId, cancellationToken);
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
        var supportCase = await LoadLatestSupportCaseAsync(orderId, cancellationToken);
        if (supportCase is not null)
        {
            await _orderSupportCaseWorkflowService.ResolveAsync(
                supportCase.Id,
                GetRequiredAdminUserId(),
                "Operational case closed by admin.",
                cancellationToken);

            return Ok(await RequireDetailAsync(orderId, cancellationToken));
        }

        var complaint = await LoadLatestLegacyComplaintAsync(orderId, cancellationToken);
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
        var supportCase = await LoadLatestSupportCaseAsync(orderId, cancellationToken);
        if (supportCase is not null)
        {
            await _orderSupportCaseWorkflowService.ReopenAsync(
                supportCase.Id,
                GetRequiredAdminUserId(),
                "Operational case reopened by admin.",
                cancellationToken);

            return Ok(await RequireDetailAsync(orderId, cancellationToken));
        }

        var complaint = await LoadLatestLegacyComplaintAsync(orderId, cancellationToken);
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
        return await _dbContext.Orders
            .Include(item => item.Vendor)
            .Include(item => item.VendorBranch)
            .FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);
    }

    private Task<OrderSupportCase?> LoadActiveSupportCaseAsync(Guid orderId, CancellationToken cancellationToken) =>
        _dbContext.OrderSupportCases
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(
                item => item.OrderId == orderId &&
                        item.Status != OrderSupportCaseStatus.Rejected &&
                        item.Status != OrderSupportCaseStatus.Resolved,
                cancellationToken);

    private Task<OrderSupportCase?> LoadLatestSupportCaseAsync(Guid orderId, CancellationToken cancellationToken) =>
        _dbContext.OrderSupportCases
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);

    private Task<OrderComplaint?> LoadLatestLegacyComplaintAsync(Guid orderId, CancellationToken cancellationToken) =>
        _dbContext.OrderComplaints
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);

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

    private async Task<bool> DriverMatchesDeliveryAreaAsync(Driver driver, Order order, CancellationToken cancellationToken)
    {
        var pickupCity = FirstNonBlank(order.VendorBranch?.City, order.Vendor?.City);
        var customerAddress = await _dbContext.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == order.CustomerAddressId, cancellationToken);

        return DeliveryPickupAreaMatcher.DriverMatchesDeliveryArea(
            driver,
            pickupCity,
            customerAddress?.City);
    }

    private static bool CityMatches(string? left, string? right)
    {
        var normalizedLeft = NormalizeCity(left);
        var normalizedRight = NormalizeCity(right);

        return !string.IsNullOrWhiteSpace(normalizedLeft)
            && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return null;
        }

        var normalized = city.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);

        if (normalized.StartsWith("ال", StringComparison.Ordinal) && normalized.Length > 2)
        {
            normalized = normalized[2..];
        }
        else if (normalized.StartsWith("al", StringComparison.Ordinal) && normalized.Length > 2)
        {
            normalized = normalized[2..];
        }

        return normalized;
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string BuildSupportCaseMessage(string title, params string?[] segments)
    {
        var parts = new[] { title }
            .Concat(segments)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim());

        return string.Join(" ", parts);
    }

    private static IReadOnlyList<OrderSupportCaseAttachmentInput> BuildSupportCaseAttachments(
        IReadOnlyList<AdminDisputeAttachmentRequest>? attachments)
    {
        return attachments?
            .Where(item => !string.IsNullOrWhiteSpace(item.FileName) && !string.IsNullOrWhiteSpace(item.FileUrl))
            .Select(item => new OrderSupportCaseAttachmentInput(item.FileName!.Trim(), item.FileUrl!.Trim()))
            .ToList()
            ?? [];
    }

    private static string? BuildDisputeInternalNote(AdminDisputeOrderRequest request)
    {
        if (!request.AddToLog && !request.MarkHighRisk)
        {
            return null;
        }

        return BuildSupportCaseMessage(
            request.MarkHighRisk ? "High-risk dispute marker requested." : "Admin dispute log entry.",
            string.IsNullOrWhiteSpace(request.DisputeType) ? null : $"Dispute type: {request.DisputeType}.",
            string.IsNullOrWhiteSpace(request.RouteTo) ? null : $"Requested route: {request.RouteTo}.",
            string.IsNullOrWhiteSpace(request.Priority) ? null : $"Requested priority: {request.Priority}.",
            request.Description,
            request.InternalNotes,
            request.MarkHighRisk ? "Priority forced to critical and routed to risk queue." : null);
    }

    private static string BuildDisputeEscalationNote(AdminDisputeOrderRequest request, string? internalNote)
    {
        return internalNote ?? BuildSupportCaseMessage(
            "Admin dispute escalated.",
            string.IsNullOrWhiteSpace(request.DisputeType) ? null : $"Dispute type: {request.DisputeType}.",
            request.Description);
    }

    private static string BuildIssueInternalNote(AdminIssueFlagRequest request)
    {
        return BuildSupportCaseMessage(
            "Operational issue internal note.",
            string.IsNullOrWhiteSpace(request.IssueType) ? null : $"Issue type: {request.IssueType}.",
            string.IsNullOrWhiteSpace(request.AssignedTeam) ? null : $"Assigned team: {request.AssignedTeam}.",
            string.IsNullOrWhiteSpace(request.Priority) ? null : $"Requested priority: {request.Priority}.",
            string.IsNullOrWhiteSpace(request.FollowUpDate) ? null : $"Follow-up date: {request.FollowUpDate}.",
            request.ShowInOperationsCenter ? "Operations center visibility requested." : "Internal note only; operations center visibility not requested.",
            request.NotifyAssignedTeam ? "Assigned team notification requested." : "Assigned team notification skipped.",
            request.HighRiskAlert ? "High-risk alert requested; priority forced to critical and routed to risk queue." : null,
            request.RequiredAction);
    }

    private static string? ResolveAdminQueue(string? routeTo)
    {
        return routeTo?.Trim().ToLowerInvariant() switch
        {
            "finance" => "finance",
            "operations" => "operations",
            "risk" => "risk",
            "legal" => "legal",
            "support" => "support",
            _ => "support"
        };
    }

    private static string ResolveIssueQueue(string? assignedTeam)
    {
        return assignedTeam?.Trim().ToLowerInvariant() switch
        {
            "finance" => "finance",
            "operations" => "operations",
            "compliance" => "risk",
            _ => "support"
        };
    }

    private async Task EnsureRefundAsync(
        Order order,
        Guid adminUserId,
        decimal amount,
        string? reason,
        CancellationToken cancellationToken)
    {
        var refundAmount = Math.Min(amount, order.TotalAmount);
        var payment = await _dbContext.Payments
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(item => item.OrderId == order.Id, cancellationToken);

        if (payment is null)
        {
            payment = new Payment(order.Id, order.PaymentMethod, order.TotalAmount);
            payment.MarkAsPaid();
            _dbContext.Payments.Add(payment);
        }

        var supportCase = await _dbContext.OrderSupportCases
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(
                item => item.OrderId == order.Id &&
                        item.Type == OrderSupportCaseType.ReturnRequest &&
                        item.Status != OrderSupportCaseStatus.Rejected &&
                        item.Status != OrderSupportCaseStatus.Resolved,
                cancellationToken);

        if (supportCase is null)
        {
            supportCase = new OrderSupportCase(
                order.Id,
                order.UserId,
                OrderSupportCaseType.ReturnRequest,
                OrderSupportCasePriority.High,
                OrderSupportCaseQueue.Finance,
                "admin_refund",
                BuildSupportCaseMessage("Admin cancellation refund recorded.", reason),
                DateTime.UtcNow.AddHours(8),
                refundAmount,
                "admin");

            _dbContext.OrderSupportCases.Add(supportCase);
        }

        if (supportCase.Status != OrderSupportCaseStatus.Approved)
        {
            supportCase.Approve(
                adminUserId,
                refundAmount,
                "same_method",
                OrderSupportCaseCompensationType.CashRefund,
                compensationCouponId: null,
                "Platform",
                reason,
                reason);
        }

        var refund = await _dbContext.Refunds
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(item => item.OrderSupportCaseId == supportCase.Id, cancellationToken);

        if (refund is null)
        {
            refund = new Refund(payment.Id, refundAmount, reason, "same_method", "Platform", supportCase.Id);
            _dbContext.Refunds.Add(refund);
        }
        else
        {
            refund.UpdateDecision(refundAmount, reason, "same_method", "Platform", supportCase.Id);
        }

        refund.Process();
        order.UpdatePaymentStatus(refundAmount >= order.TotalAmount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded);
    }

    private static void EnsurePickupOtpForAdminAssignment(
        DeliveryAssignment assignment,
        Guid? previousDriverId,
        Guid assignedDriverId)
    {
        if (assignment.IsPickupOtpVerified)
        {
            return;
        }

        if (previousDriverId.HasValue &&
            previousDriverId.Value != assignedDriverId &&
            !string.IsNullOrWhiteSpace(assignment.PickupOtpCode))
        {
            assignment.RegeneratePickupOtp(PickupOtpTtl);
            return;
        }

        assignment.EnsurePickupOtp(PickupOtpTtl);
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
    IReadOnlyList<AdminDisputeAttachmentRequest>? Attachments,
    bool NotifyReviewer,
    bool AddToLog,
    bool MarkHighRisk,
    bool NotifyStakeholders);

public record AdminDisputeAttachmentRequest(
    string? FileName,
    string? FileUrl);

public record AdminIssueFlagRequest(
    string? IssueType,
    string? Priority,
    string? RequiredAction,
    string? AssignedTeam,
    string? FollowUpDate,
    bool ShowInOperationsCenter,
    bool NotifyAssignedTeam,
    bool HighRiskAlert);
