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
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == customerUserId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        var supportCaseType = ParseType(type);
        ValidateCustomerCreateEligibility(order, supportCaseType);
        await EnsureNoActiveCaseAsync(order.Id, cancellationToken);

        var supportCase = new OrderSupportCase(
            order.Id,
            customerUserId,
            supportCaseType,
            ResolvePriority(supportCaseType, reasonCode, null),
            ResolveQueue(supportCaseType, reasonCode, null),
            reasonCode,
            message,
            ResolveSlaDueAt(supportCaseType, null),
            supportCaseType == OrderSupportCaseType.ReturnRequest ? order.TotalAmount : null);

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
        await NotifyCustomerAsync(order, supportCase, "created", cancellationToken);
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
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        var supportCaseType = ParseType(type);
        await EnsureNoActiveCaseAsync(order.Id, cancellationToken);

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
        DateTime? slaDueAtUtc,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await LoadCaseForWriteAsync(caseId, cancellationToken);
        supportCase.RequestCustomerEvidence(actorUserId, note, customerVisibleNote, slaDueAtUtc);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyCustomerAsync(supportCase.Order, supportCase, "request_evidence", cancellationToken);
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
        }

        return supportCase;
    }

    private async Task<OrderSupportCase> LoadCaseForWriteAsync(Guid caseId, CancellationToken cancellationToken)
    {
        return await _context.OrderSupportCases
            .Include(x => x.Order)
            .Include(x => x.Attachments)
            .Include(x => x.Activities)
            .FirstOrDefaultAsync(x => x.Id == caseId, cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", caseId);
    }

    private async Task EnsureNoActiveCaseAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var hasActiveCase = await _context.OrderSupportCases
            .AnyAsync(x => x.OrderId == orderId && x.Status != OrderSupportCaseStatus.Rejected && x.Status != OrderSupportCaseStatus.Resolved, cancellationToken);

        if (hasActiveCase)
        {
            throw new BusinessRuleException("ORDER_SUPPORT_CASE_ALREADY_EXISTS", "An active support case already exists for this order.");
        }
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
            "complaint" or "issue" or "dispute" => OrderSupportCaseType.Complaint,
            _ => throw new BusinessRuleException("INVALID_SUPPORT_CASE_TYPE", "Support case type must be complaint or return_request.")
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
            _ => null
        };
    }

    private static string? NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
