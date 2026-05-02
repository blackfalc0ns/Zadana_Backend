using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Services;

public sealed class OrderSupportCaseWorkflowService : IOrderSupportCaseWorkflowService
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public OrderSupportCaseWorkflowService(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
    }

    public async Task<OrderSupportCase> CreateCustomerCaseAsync(
        Guid orderId,
        Guid customerUserId,
        string type,
        string? reasonCode,
        string message,
        IReadOnlyList<OrderSupportCaseAttachmentInput>? attachments,
        CancellationToken cancellationToken = default)
    {
        var supportCaseType = ParseType(type);
        var isDriverInitiated = supportCaseType is OrderSupportCaseType.DriverReport or OrderSupportCaseType.DriverDispute;

        Order order;
        if (isDriverInitiated)
        {
            // Driver-initiated: verify driver has an assignment for this order
            order = await _context.Orders
                .Include(x => x.User)
                .Include(x => x.Vendor)
                .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
                ?? throw new NotFoundException("Order", orderId);

            var hasAssignment = await _context.DeliveryAssignments
                .AnyAsync(a => a.Order.Id == orderId && a.Driver.UserId == customerUserId, cancellationToken);

            if (!hasAssignment)
            {
                throw new BusinessRuleException("NOT_ASSIGNED_TO_ORDER", "You can only report issues for orders assigned to you.");
            }
        }
        else
        {
            // Customer-initiated: verify order ownership
            order = await _context.Orders
                .Include(x => x.User)
                .Include(x => x.Vendor)
                .FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == customerUserId, cancellationToken)
                ?? throw new NotFoundException("Order", orderId);

            ValidateCustomerCreateEligibility(order, supportCaseType);
        }

        var initiatorRole = isDriverInitiated ? "driver" : "customer";
        var activeCase = await LoadActiveCaseAsync(order.Id, cancellationToken);

        if (activeCase is not null)
        {
            activeCase.MergeIntoActiveCase(
                customerUserId,
                initiatorRole,
                message,
                attachments?.Select(item => (item.FileName, item.FileUrl)).ToList());

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await NotifyAdminRecipientsAsync(
                order,
                activeCase,
                "updated",
                actorUserId: customerUserId,
                notifyEscalatedTeam: true,
                notifyCurrentReviewer: true,
                cancellationToken);

            if (!isDriverInitiated)
            {
                await NotifyVendorAsync(order, activeCase, "customer_updated", cancellationToken);
            }

            return activeCase;
        }

        var supportCase = new OrderSupportCase(
            order.Id,
            customerUserId,
            supportCaseType,
            ResolvePriority(supportCaseType, reasonCode, null),
            ResolveQueue(supportCaseType, reasonCode, null),
            reasonCode,
            message,
            ResolveSlaDueAt(supportCaseType, null),
            supportCaseType == OrderSupportCaseType.ReturnRequest ? order.TotalAmount : null,
            initiatorRole);

        foreach (var attachment in attachments ?? [])
        {
            supportCase.AddAttachment(attachment.FileName, attachment.FileUrl, customerUserId);
        }

        _context.OrderSupportCases.Add(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyAdminRecipientsAsync(
            order,
            supportCase,
            "created",
            actorUserId: customerUserId,
            notifyEscalatedTeam: true,
            notifyCurrentReviewer: false,
            cancellationToken);

        if (!isDriverInitiated)
        {
            await NotifyCustomerAsync(order, supportCase, "created", cancellationToken);
            await NotifyVendorAsync(order, supportCase, "customer_created", cancellationToken);
        }
        else
        {
            await NotifyDriverAsync(order, supportCase, customerUserId, "created", cancellationToken);
        }

        return supportCase;
    }

    public async Task<OrderSupportCase> CreateAdminCaseAsync(
        Guid orderId,
        Guid adminUserId,
        string type,
        string? reasonCode,
        string message,
        string? priority,
        string? queue,
        string? internalNote,
        string? customerVisibleNote,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(x => x.User)
            .Include(x => x.Vendor)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        var supportCaseType = ParseType(type);
        var activeCase = await LoadActiveCaseAsync(order.Id, cancellationToken);

        if (activeCase is not null)
        {
            activeCase.AddAdminPublicMessage(adminUserId, message, "customer,vendor");

            if (!string.IsNullOrWhiteSpace(internalNote))
            {
                activeCase.AddInternalNote(adminUserId, internalNote, visibleToCustomer: false);
            }

            if (!string.IsNullOrWhiteSpace(customerVisibleNote))
            {
                activeCase.AddAdminPublicMessage(adminUserId, customerVisibleNote, "customer,vendor");
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return activeCase;
        }

        var supportCase = new OrderSupportCase(
            order.Id,
            order.UserId,
            supportCaseType,
            ResolvePriority(supportCaseType, reasonCode, priority),
            ResolveQueue(supportCaseType, reasonCode, queue),
            reasonCode,
            message,
            ResolveSlaDueAt(supportCaseType, priority),
            supportCaseType == OrderSupportCaseType.ReturnRequest ? order.TotalAmount : null);

        _context.OrderSupportCases.Add(supportCase);
        supportCase.Assign(adminUserId, adminUserId, internalNote, ResolvePriority(supportCaseType, reasonCode, priority), ResolveSlaDueAt(supportCaseType, priority));

        if (!string.IsNullOrWhiteSpace(customerVisibleNote))
        {
            supportCase.AddInternalNote(adminUserId, customerVisibleNote, visibleToCustomer: true);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return supportCase;
    }

    public async Task<OrderSupportCase> AssignAsync(
        Guid caseId,
        Guid actorUserId,
        Guid? assignedAdminId,
        string? note,
        string? priority,
        DateTime? slaDueAtUtc,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadCaseForWriteAsync(caseId, cancellationToken);
        supportCase.Assign(actorUserId, assignedAdminId, note, ParsePriority(priority) ?? supportCase.Priority, slaDueAtUtc);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (supportCase.AssignedAdminId.HasValue && supportCase.AssignedAdminId.Value != actorUserId)
        {
            await NotifySpecificAdminAsync(supportCase.Order, supportCase, supportCase.AssignedAdminId.Value, "assigned", cancellationToken);
        }

        return supportCase;
    }

    public async Task<OrderSupportCase> RequestEvidenceAsync(
        Guid caseId,
        Guid actorUserId,
        string? note,
        string? customerVisibleNote,
        string? targetRole,
        DateTime? slaDueAtUtc,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadCaseForWriteAsync(caseId, cancellationToken);
        var normalizedTargetRole = NormalizeToken(targetRole) switch
        {
            "merchant" => "vendor",
            "customer" => "customer",
            "vendor" => "vendor",
            "driver" => "driver",
            _ => "customer"
        };

        supportCase.RequestEvidenceFrom(actorUserId, normalizedTargetRole, note, customerVisibleNote, slaDueAtUtc);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        switch (normalizedTargetRole)
        {
            case "vendor":
                await NotifyVendorAsync(supportCase.Order, supportCase, "request_evidence", cancellationToken);
                break;
            case "driver":
                await NotifyActiveDriverAsync(supportCase.Order, supportCase, "request_evidence", cancellationToken);
                break;
            default:
                await NotifyCustomerAsync(supportCase.Order, supportCase, "request_evidence", cancellationToken);
                break;
        }

        return supportCase;
    }

    public async Task<OrderSupportCase> EscalateAsync(
        Guid caseId,
        Guid actorUserId,
        string? queue,
        string? priority,
        string? note,
        string? customerVisibleNote,
        bool notifyEscalatedTeam,
        bool notifyCurrentReviewer,
        DateTime? slaDueAtUtc,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadCaseForWriteAsync(caseId, cancellationToken);
        var resolvedQueue = ParseQueue(queue) ?? supportCase.Queue;
        var resolvedPriority = ParsePriority(priority) ?? supportCase.Priority;

        supportCase.Escalate(
            actorUserId,
            resolvedQueue,
            resolvedPriority,
            note,
            customerVisibleNote,
            slaDueAtUtc);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyAdminRecipientsAsync(
            supportCase.Order,
            supportCase,
            "escalated",
            actorUserId,
            notifyEscalatedTeam,
            notifyCurrentReviewer,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(customerVisibleNote))
        {
            await NotifyCustomerAsync(supportCase.Order, supportCase, "escalated", cancellationToken);
        }

        return supportCase;
    }

    public async Task<OrderSupportCase> ApproveAsync(
        Guid caseId,
        Guid actorUserId,
        decimal? refundAmount,
        string? refundMethod,
        string? costBearer,
        string? decisionNotes,
        string? customerVisibleNote,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadCaseForWriteAsync(caseId, cancellationToken);
        var approvedAmount = ResolveApprovalAmount(supportCase, refundAmount);

        supportCase.Approve(actorUserId, approvedAmount, refundMethod, costBearer, decisionNotes, customerVisibleNote);

        if (supportCase.Type == OrderSupportCaseType.ReturnRequest)
        {
            await EnsureRefundDecisionAsync(supportCase, approvedAmount, refundMethod, costBearer, decisionNotes, actorUserId, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyCustomerAsync(supportCase.Order, supportCase, "approved", cancellationToken);
        await NotifyVendorAsync(supportCase.Order, supportCase, "approved", cancellationToken);
        await NotifyActiveDriverAsync(supportCase.Order, supportCase, "approved", cancellationToken);
        return supportCase;
    }

    public async Task<OrderSupportCase> RejectAsync(
        Guid caseId,
        Guid actorUserId,
        string? decisionNotes,
        string? customerVisibleNote,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadCaseForWriteAsync(caseId, cancellationToken);
        supportCase.Reject(actorUserId, decisionNotes, customerVisibleNote);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyCustomerAsync(supportCase.Order, supportCase, "rejected", cancellationToken);
        await NotifyVendorAsync(supportCase.Order, supportCase, "rejected", cancellationToken);
        await NotifyActiveDriverAsync(supportCase.Order, supportCase, "rejected", cancellationToken);
        return supportCase;
    }

    public async Task<OrderSupportCase> ResolveAsync(
        Guid caseId,
        Guid actorUserId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadCaseForWriteAsync(caseId, cancellationToken);
        supportCase.Resolve(actorUserId, note);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyCustomerAsync(supportCase.Order, supportCase, "resolved", cancellationToken);
        await NotifyVendorAsync(supportCase.Order, supportCase, "resolved", cancellationToken);
        await NotifyActiveDriverAsync(supportCase.Order, supportCase, "resolved", cancellationToken);
        return supportCase;
    }

    public async Task<OrderSupportCase> ReopenAsync(
        Guid caseId,
        Guid actorUserId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadCaseForWriteAsync(caseId, cancellationToken);
        supportCase.Reopen(actorUserId, note);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return supportCase;
    }

    public async Task<OrderSupportCase> AddNoteAsync(
        Guid caseId,
        Guid actorUserId,
        string note,
        bool visibleToCustomer,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadCaseForWriteAsync(caseId, cancellationToken);
        supportCase.AddInternalNote(actorUserId, note, visibleToCustomer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (visibleToCustomer)
        {
            await NotifyCustomerAsync(supportCase.Order, supportCase, "note_added", cancellationToken);
            await NotifyVendorAsync(supportCase.Order, supportCase, "note_added", cancellationToken);
        }

        return supportCase;
    }

    public async Task<OrderSupportCase> AddVendorResponseAsync(
        Guid caseId,
        Guid vendorUserId,
        string response,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadCaseForWriteAsync(caseId, cancellationToken);
        supportCase.AddVendorResponse(vendorUserId, response);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyAdminRecipientsAsync(
            supportCase.Order,
            supportCase,
            "vendor_responded",
            actorUserId: vendorUserId,
            notifyEscalatedTeam: false,
            notifyCurrentReviewer: true,
            cancellationToken);

        await NotifyCustomerAsync(supportCase.Order, supportCase, "vendor_responded", cancellationToken);

        return supportCase;
    }

    public async Task<OrderSupportCase> AddDriverResponseAsync(
        Guid caseId,
        Guid orderId,
        Guid driverUserId,
        string response,
        IReadOnlyList<OrderSupportCaseAttachmentInput>? attachments,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await _context.OrderSupportCases
            .Include(item => item.Order)
                .ThenInclude(order => order.User)
            .Include(item => item.Order)
                .ThenInclude(order => order.Vendor)
            .Include(item => item.Activities)
            .Include(item => item.Attachments)
            .FirstOrDefaultAsync(item => item.Id == caseId && item.OrderId == orderId, cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", caseId);

        var assignedDriverUserIds = await _context.DeliveryAssignments
            .Where(item => item.OrderId == orderId && item.Driver != null)
            .Select(item => item.Driver!.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (!assignedDriverUserIds.Contains(driverUserId))
        {
            throw new ForbiddenAccessException("You are not assigned to this order.");
        }

        supportCase.MergeIntoActiveCase(
            driverUserId,
            "driver",
            response,
            attachments?.Select(item => (item.FileName, item.FileUrl)).ToList());

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyAdminRecipientsAsync(
            supportCase.Order,
            supportCase,
            "driver_responded",
            actorUserId: driverUserId,
            notifyEscalatedTeam: false,
            notifyCurrentReviewer: true,
            cancellationToken);

        return supportCase;
    }

    public async Task<OrderSupportCase> AddAdminPublicMessageAsync(
        Guid caseId,
        Guid actorUserId,
        string message,
        string audience,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadCaseForWriteAsync(caseId, cancellationToken);
        supportCase.AddAdminPublicMessage(actorUserId, message, audience);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (AudienceIncludes(audience, "customer"))
        {
            await NotifyCustomerAsync(supportCase.Order, supportCase, "admin_message", cancellationToken);
        }

        if (AudienceIncludes(audience, "vendor"))
        {
            await NotifyVendorAsync(supportCase.Order, supportCase, "admin_message", cancellationToken);
        }

        if (AudienceIncludes(audience, "driver"))
        {
            await NotifyActiveDriverAsync(supportCase.Order, supportCase, "admin_message", cancellationToken);
        }

        return supportCase;
    }

    private async Task<OrderSupportCase> LoadCaseForWriteAsync(Guid caseId, CancellationToken cancellationToken)
    {
        return await _context.OrderSupportCases
            .Include(x => x.Order)
                .ThenInclude(order => order.User)
            .Include(x => x.Order)
                .ThenInclude(order => order.Vendor)
            .Include(x => x.Attachments)
            .Include(x => x.Activities)
            .FirstOrDefaultAsync(x => x.Id == caseId, cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", caseId);
    }

    private async Task<OrderSupportCase?> LoadActiveCaseAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await _context.OrderSupportCases
            .Include(x => x.Order)
                .ThenInclude(order => order.User)
            .Include(x => x.Order)
                .ThenInclude(order => order.Vendor)
            .Include(x => x.Attachments)
            .Include(x => x.Activities)
            .Where(x => x.OrderId == orderId && x.Status != OrderSupportCaseStatus.Rejected && x.Status != OrderSupportCaseStatus.Resolved)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static void ValidateCustomerCreateEligibility(Order order, OrderSupportCaseType supportCaseType)
    {
        if (supportCaseType == OrderSupportCaseType.Complaint)
        {
            if (order.Status == OrderStatus.PendingPayment)
            {
                throw new BusinessRuleException("ORDER_COMPLAINT_NOT_ALLOWED", "Complaints can only be created after the order leaves pending payment.");
            }

            return;
        }

        if (order.Status != OrderStatus.Delivered)
        {
            throw new BusinessRuleException("ORDER_RETURN_NOT_ALLOWED", "Return requests can only be created for delivered orders.");
        }
    }

    private async Task EnsureRefundDecisionAsync(
        OrderSupportCase supportCase,
        decimal? approvedAmount,
        string? refundMethod,
        string? costBearer,
        string? decisionNotes,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (!approvedAmount.HasValue || approvedAmount.Value <= 0)
        {
            throw new BusinessRuleException("RETURN_REFUND_AMOUNT_REQUIRED", "Return requests require a refund amount when approved.");
        }

        var order = supportCase.Order;
        var payment = await _context.Payments
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(item => item.OrderId == order.Id, cancellationToken);

        if (payment is null)
        {
            payment = new Payment(order.Id, order.PaymentMethod, order.TotalAmount);
            payment.MarkAsPaid();
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var refund = await _context.Refunds
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(item => item.OrderSupportCaseId == supportCase.Id, cancellationToken);

        var boundedAmount = Math.Min(approvedAmount.Value, order.TotalAmount);

        if (refund is null)
        {
            refund = new Refund(payment.Id, boundedAmount, decisionNotes, refundMethod, costBearer, supportCase.Id);
            _context.Refunds.Add(refund);
        }
        else
        {
            refund.UpdateDecision(boundedAmount, decisionNotes, refundMethod, costBearer, supportCase.Id);
        }

        refund.Process();

        order.UpdatePaymentStatus(boundedAmount >= order.TotalAmount
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded);

        if (order.Status != OrderStatus.Refunded)
        {
            order.ChangeStatus(OrderStatus.Refunded, actorUserId, decisionNotes ?? "Support case approved as return request.");
            _context.OrderStatusHistories.Add(order.StatusHistory.Last());
        }
    }

    private async Task NotifyCustomerAsync(
        Order order,
        OrderSupportCase supportCase,
        string action,
        CancellationToken cancellationToken)
    {
        var composed = OrderSupportCaseNotificationComposer.ComposeCustomer(
            order.Id,
            supportCase.Id,
            order.OrderNumber,
            supportCase.Type,
            supportCase.Status,
            action);

        await _notificationService.SendToUserAsync(
            order.UserId,
            composed.TitleAr,
            composed.TitleEn,
            composed.BodyAr,
            composed.BodyEn,
            composed.NotificationType,
            supportCase.Id,
            composed.Data,
            cancellationToken);

        await _notificationService.SendOrderSupportCaseChangedToUserAsync(
            order.UserId,
            supportCase.Id,
            order.Id,
            order.OrderNumber,
            OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Type),
            OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Status),
            action,
            composed.TargetUrl,
            cancellationToken);

        await OneSignalMobilePushRequest.CreateHeadsUp(
                order.UserId.ToString(),
                composed.TitleAr,
                composed.TitleEn,
                composed.BodyAr,
                composed.BodyEn,
                composed.NotificationType,
                supportCase.Id,
                composed.Data,
                composed.TargetUrl)
            .DispatchAsync(_oneSignalPushService, cancellationToken);
    }

    private async Task NotifyVendorAsync(
        Order order,
        OrderSupportCase supportCase,
        string action,
        CancellationToken cancellationToken)
    {
        if (order.Vendor?.UserId is not Guid vendorUserId || vendorUserId == Guid.Empty)
        {
            return;
        }

        await _notificationService.SendToUserAsync(
            vendorUserId,
            "تحديث في نزاع طلب",
            "Order dispute updated",
            $"تم تحديث حالة النزاع للطلب #{order.OrderNumber}.",
            $"The dispute for order #{order.OrderNumber} has been updated.",
            "order_support_case",
            supportCase.Id,
            $$"""{"action":"{{action}}","orderId":"{{order.Id}}","caseId":"{{supportCase.Id}}"}""",
            cancellationToken);
    }

    private async Task NotifyActiveDriverAsync(
        Order order,
        OrderSupportCase supportCase,
        string action,
        CancellationToken cancellationToken)
    {
        var driverUserId = await _context.DeliveryAssignments
            .AsNoTracking()
            .Where(item => item.OrderId == order.Id && item.Driver != null)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.Driver!.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (driverUserId == Guid.Empty)
        {
            return;
        }

        await NotifyDriverAsync(order, supportCase, driverUserId, action, cancellationToken);
    }

    private async Task NotifyDriverAsync(
        Order order,
        OrderSupportCase supportCase,
        Guid driverUserId,
        string action,
        CancellationToken cancellationToken)
    {
        if (driverUserId == Guid.Empty)
        {
            return;
        }

        await _notificationService.SendToUserAsync(
            driverUserId,
            "تحديث في بلاغ الطلب",
            "Order case updated",
            $"هناك تحديث جديد على الحالة المرتبطة بالطلب #{order.OrderNumber}.",
            $"There is a new update on the case for order #{order.OrderNumber}.",
            "order_support_case",
            supportCase.Id,
            $$"""{"action":"{{action}}","orderId":"{{order.Id}}","caseId":"{{supportCase.Id}}"}""",
            cancellationToken);
    }

    private async Task NotifyAdminRecipientsAsync(
        Order order,
        OrderSupportCase supportCase,
        string action,
        Guid actorUserId,
        bool notifyEscalatedTeam,
        bool notifyCurrentReviewer,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<Guid>();

        if (notifyCurrentReviewer && supportCase.AssignedAdminId.HasValue && supportCase.AssignedAdminId.Value != actorUserId)
        {
            recipients.Add(supportCase.AssignedAdminId.Value);
        }

        if (notifyEscalatedTeam)
        {
            var adminRecipients = await _context.Users
                .AsNoTracking()
                .Where(user =>
                    user.Id != actorUserId &&
                    user.AccountStatus == AccountStatus.Active &&
                    (user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);

            foreach (var recipient in adminRecipients)
            {
                recipients.Add(recipient);
            }
        }

        if (recipients.Count == 0)
        {
            return;
        }

        var composed = OrderSupportCaseNotificationComposer.ComposeAdmin(
            order.Id,
            supportCase.Id,
            order.OrderNumber,
            supportCase.Type,
            supportCase.Status,
            supportCase.Queue,
            supportCase.Priority,
            action);

        foreach (var recipientId in recipients)
        {
            await _notificationService.SendToUserAsync(
                recipientId,
                composed.TitleAr,
                composed.TitleEn,
                composed.BodyAr,
                composed.BodyEn,
                composed.NotificationType,
                supportCase.Id,
                composed.Data,
                cancellationToken);
        }
    }

    private async Task NotifySpecificAdminAsync(
        Order order,
        OrderSupportCase supportCase,
        Guid adminUserId,
        string action,
        CancellationToken cancellationToken)
    {
        var composed = OrderSupportCaseNotificationComposer.ComposeAdmin(
            order.Id,
            supportCase.Id,
            order.OrderNumber,
            supportCase.Type,
            supportCase.Status,
            supportCase.Queue,
            supportCase.Priority,
            action);

        await _notificationService.SendToUserAsync(
            adminUserId,
            composed.TitleAr,
            composed.TitleEn,
            composed.BodyAr,
            composed.BodyEn,
            composed.NotificationType,
            supportCase.Id,
            composed.Data,
            cancellationToken);
    }

    private static decimal? ResolveApprovalAmount(OrderSupportCase supportCase, decimal? requestedAmount)
    {
        if (requestedAmount.HasValue)
        {
            return requestedAmount.Value;
        }

        return supportCase.ApprovedRefundAmount
            ?? supportCase.RequestedRefundAmount
            ?? supportCase.Order.TotalAmount;
    }

    private static OrderSupportCaseType ParseType(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "return_request" or "return" or "refund" => OrderSupportCaseType.ReturnRequest,
            "complaint" or "issue" => OrderSupportCaseType.Complaint,
            "driver_report" => OrderSupportCaseType.DriverReport,
            "driver_dispute" or "dispute" => OrderSupportCaseType.DriverDispute,
            _ => throw new BusinessRuleException("INVALID_SUPPORT_CASE_TYPE", "Support case type is not recognized.")
        };

    private static OrderSupportCasePriority ResolvePriority(OrderSupportCaseType type, string? reasonCode, string? explicitPriority)
    {
        var parsed = ParsePriority(explicitPriority);
        if (parsed.HasValue)
        {
            return parsed.Value;
        }

        var normalizedReason = NormalizeToken(reasonCode);

        if (type == OrderSupportCaseType.ReturnRequest || normalizedReason == "payment_issue")
        {
            return OrderSupportCasePriority.High;
        }

        if (normalizedReason is "fraud" or "fraud_suspicion")
        {
            return OrderSupportCasePriority.Critical;
        }

        if (normalizedReason is "delivery_delay" or "prep_delay")
        {
            return OrderSupportCasePriority.Medium;
        }

        return OrderSupportCasePriority.Medium;
    }

    private static OrderSupportCaseQueue ResolveQueue(OrderSupportCaseType type, string? reasonCode, string? explicitQueue)
    {
        var parsed = ParseQueue(explicitQueue);
        if (parsed.HasValue)
        {
            return parsed.Value;
        }

        var normalizedReason = NormalizeToken(reasonCode);

        if (type == OrderSupportCaseType.ReturnRequest || normalizedReason == "payment_issue")
        {
            return OrderSupportCaseQueue.Finance;
        }

        if (normalizedReason is "delivery_delay" or "prep_delay")
        {
            return OrderSupportCaseQueue.Operations;
        }

        if (type is OrderSupportCaseType.DriverReport or OrderSupportCaseType.DriverDispute)
        {
            return normalizedReason == "payout_dispute" ? OrderSupportCaseQueue.Finance : OrderSupportCaseQueue.DriverOps;
        }

        return OrderSupportCaseQueue.Support;
    }

    private static DateTime ResolveSlaDueAt(OrderSupportCaseType type, string? priority)
    {
        var parsedPriority = ParsePriority(priority);
        var hours = parsedPriority switch
        {
            OrderSupportCasePriority.Critical => 4,
            OrderSupportCasePriority.High => 8,
            OrderSupportCasePriority.Low => 24,
            _ => type == OrderSupportCaseType.ReturnRequest ? 12 : 16
        };

        return DateTime.UtcNow.AddHours(hours);
    }

    private static OrderSupportCasePriority? ParsePriority(string? value)
    {
        return NormalizeToken(value) switch
        {
            "low" => OrderSupportCasePriority.Low,
            "medium" => OrderSupportCasePriority.Medium,
            "high" => OrderSupportCasePriority.High,
            "critical" => OrderSupportCasePriority.Critical,
            _ => null
        };
    }

    private static OrderSupportCaseQueue? ParseQueue(string? value)
    {
        return NormalizeToken(value) switch
        {
            "support" => OrderSupportCaseQueue.Support,
            "finance" => OrderSupportCaseQueue.Finance,
            "operations" => OrderSupportCaseQueue.Operations,
            "risk" => OrderSupportCaseQueue.Risk,
            "legal" => OrderSupportCaseQueue.Legal,
            "driver_ops" or "driverops" => OrderSupportCaseQueue.DriverOps,
            _ => null
        };
    }

    private static string? NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static bool AudienceIncludes(string audience, string role)
    {
        var normalizedAudience = NormalizeToken(audience) ?? "all_external";
        if (normalizedAudience == "all_external")
        {
            return true;
        }

        return normalizedAudience
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, role, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<OrderSupportCase> AddCustomerReplyAsync(
        Guid caseId,
        Guid orderId,
        Guid customerUserId,
        string message,
        IReadOnlyList<OrderSupportCaseAttachmentInput>? attachments,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await _context.OrderSupportCases
            .Include(item => item.Order)
                .ThenInclude(order => order.User)
            .Include(item => item.Order)
                .ThenInclude(order => order.Vendor)
            .Include(item => item.Activities)
            .Include(item => item.Attachments)
            .FirstOrDefaultAsync(item => item.Id == caseId && item.OrderId == orderId, cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", caseId);

        if (supportCase.Order.UserId != customerUserId)
        {
            throw new ForbiddenAccessException("You are not the owner of this order.");
        }

        var attachmentTuples = attachments?
            .Select(a => (a.FileName, a.FileUrl))
            .ToList();

        supportCase.AddCustomerReply(customerUserId, message, attachmentTuples);

        await _context.SaveChangesAsync(cancellationToken);

        await NotifyAdminRecipientsAsync(
            supportCase.Order,
            supportCase,
            "customer_replied",
            actorUserId: customerUserId,
            notifyEscalatedTeam: false,
            notifyCurrentReviewer: true,
            cancellationToken);

        await NotifyVendorAsync(supportCase.Order, supportCase, "customer_replied", cancellationToken);

        return supportCase;
    }
}
