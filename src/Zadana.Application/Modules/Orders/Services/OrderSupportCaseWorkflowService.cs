using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Services;

public sealed class OrderSupportCaseWorkflowService : IOrderSupportCaseWorkflowService
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly IAdminAlertService? _adminAlertService;
    private readonly VendorRecoveryService? _vendorRecoveryService;
    private readonly OrderInventoryWorkflowService _orderInventoryWorkflowService;

    public OrderSupportCaseWorkflowService(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        IAdminAlertService? adminAlertService = null,
        VendorRecoveryService? vendorRecoveryService = null,
        OrderInventoryWorkflowService? orderInventoryWorkflowService = null)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _adminAlertService = adminAlertService;
        _vendorRecoveryService = vendorRecoveryService;
        _orderInventoryWorkflowService = orderInventoryWorkflowService ?? new OrderInventoryWorkflowService(context);
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

            var driverId = await ResolveDriverIdByUserIdAsync(customerUserId, cancellationToken);

            var hasAssignment = await _context.DeliveryAssignments
                .AsNoTracking()
                .AnyAsync(a => a.OrderId == orderId && a.DriverId == driverId, cancellationToken);

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
            if (!CanMergeCustomerInitiatedCase(activeCase, supportCaseType, initiatorRole))
            {
                throw new BusinessRuleException(
                    "ORDER_SUPPORT_CASE_ALREADY_EXISTS",
                    "An active support case already exists for this order.");
            }

            activeCase.MergeIntoActiveCase(
                customerUserId,
                initiatorRole,
                message,
                attachments?.Select(item => (item.FileName, item.FileUrl)).ToList());

            StagePendingCaseArtifacts(activeCase);
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
                await NotifyVendorSupportCaseAsync(order, activeCase, "customer_updated", cancellationToken);
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
            await NotifyVendorSupportCaseAsync(order, supportCase, "customer_created", cancellationToken);
            await NotifyActiveDriverAsync(order, supportCase, "created", cancellationToken);
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
            if (!CanMergeAdminInitiatedCase(activeCase, supportCaseType))
            {
                throw new BusinessRuleException(
                    "ORDER_SUPPORT_CASE_ALREADY_EXISTS",
                    "An active support case already exists for this order.");
            }

            activeCase.AddAdminPublicMessage(adminUserId, message, "customer,vendor");

            if (!string.IsNullOrWhiteSpace(internalNote))
            {
                activeCase.AddInternalNote(adminUserId, internalNote, visibleToCustomer: false);
            }

            if (!string.IsNullOrWhiteSpace(customerVisibleNote))
            {
                activeCase.AddAdminPublicMessage(adminUserId, customerVisibleNote, "customer,vendor");
            }

            StagePendingCaseArtifacts(activeCase);
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
        await NotifyActiveDriverAsync(order, supportCase, "created", cancellationToken);
        return supportCase;
    }

    public async Task<OrderSupportCase> CreateDriverAccountAppealAsync(
        Guid driverId,
        Guid driverUserId,
        string? reasonCode,
        string message,
        IReadOnlyList<OrderSupportCaseAttachmentInput>? attachments,
        CancellationToken cancellationToken = default)
    {
        var driverExists = await _context.Drivers
            .AsNoTracking()
            .AnyAsync(item => item.Id == driverId && item.UserId == driverUserId, cancellationToken);

        if (!driverExists)
        {
            throw new NotFoundException("Driver", driverId);
        }

        var activeCase = await LoadActiveDriverAccountCaseAsync(driverId, cancellationToken);

        if (activeCase is not null)
        {
            activeCase.MergeIntoActiveCase(
                driverUserId,
                "driver",
                message,
                attachments?.Select(item => (item.FileName, item.FileUrl)).ToList());

            StagePendingCaseArtifacts(activeCase);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await NotifyDriverAccountAdminsAsync(activeCase, "updated", driverUserId, cancellationToken);
            return activeCase;
        }

        var supportCase = new OrderSupportCase(
            driverId,
            driverUserId,
            reasonCode,
            message,
            DateTime.UtcNow.AddHours(8));

        foreach (var attachment in attachments ?? [])
        {
            supportCase.AddAttachment(attachment.FileName, attachment.FileUrl, driverUserId);
        }

        _context.OrderSupportCases.Add(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyDriverAccountAdminsAsync(supportCase, "created", driverUserId, cancellationToken);

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
        StagePendingCaseArtifacts(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (supportCase.AssignedAdminId.HasValue && supportCase.AssignedAdminId.Value != actorUserId && supportCase.Order is not null)
        {
            await NotifySpecificAdminAsync(supportCase.Order, supportCase, supportCase.AssignedAdminId.Value, "assigned", cancellationToken);
        }

        if (supportCase.Order is null)
        {
            await NotifyDriverAccountDriverAsync(supportCase, "assigned", cancellationToken);
        }
        else
        {
            await NotifyActiveDriverAsync(supportCase.Order, supportCase, "assigned", cancellationToken);
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
        StagePendingCaseArtifacts(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (supportCase.Order is null)
        {
            await NotifyDriverAccountAdminsAsync(supportCase, "request_evidence", actorUserId, cancellationToken);
            if (normalizedTargetRole == "driver")
            {
                await NotifyDriverAccountDriverAsync(supportCase, "request_evidence", cancellationToken);
            }

            return supportCase;
        }

        switch (normalizedTargetRole)
        {
            case "vendor":
                await NotifyVendorSupportCaseAsync(supportCase.Order, supportCase, "request_evidence", cancellationToken);
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

        StagePendingCaseArtifacts(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (supportCase.Order is null)
        {
            await NotifyDriverAccountAdminsAsync(supportCase, "escalated", actorUserId, cancellationToken);
        }
        else
        {
            await NotifyAdminRecipientsAsync(
                supportCase.Order,
                supportCase,
                "escalated",
                actorUserId,
                notifyEscalatedTeam,
                notifyCurrentReviewer,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(customerVisibleNote) && supportCase.Order is not null)
        {
            await NotifyCustomerAsync(supportCase.Order, supportCase, "escalated", cancellationToken);
        }

        if (supportCase.Order is null)
        {
            await NotifyDriverAccountDriverAsync(supportCase, "escalated", cancellationToken);
        }
        else
        {
            await NotifyActiveDriverAsync(supportCase.Order, supportCase, "escalated", cancellationToken);
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
        var approvedAmount = supportCase.Type == OrderSupportCaseType.ReturnRequest
            ? ResolveApprovalAmount(supportCase, refundAmount)
            : refundAmount;
        var compensationDecision = supportCase.Type == OrderSupportCaseType.ReturnRequest
            ? await ResolveCompensationDecisionAsync(supportCase, approvedAmount, refundMethod, cancellationToken)
            : null;

        supportCase.Approve(
            actorUserId,
            approvedAmount,
            compensationDecision?.RefundMethod ?? refundMethod,
            compensationDecision?.CompensationType,
            compensationDecision?.Coupon?.Id,
            costBearer,
            decisionNotes,
            customerVisibleNote);

        if (supportCase.Type == OrderSupportCaseType.ReturnRequest)
        {
            await EnsureRefundDecisionAsync(
                supportCase,
                approvedAmount,
                compensationDecision!,
                costBearer,
                decisionNotes,
                actorUserId,
                cancellationToken);

            if (_vendorRecoveryService is not null && approvedAmount.HasValue)
            {
                await _vendorRecoveryService.StageRecoveryForApprovedCaseAsync(
                    supportCase,
                    approvedAmount.Value,
                    costBearer,
                    cancellationToken);
            }
        }

        StagePendingCaseArtifacts(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (supportCase.Order is null)
        {
            await NotifyDriverAccountAdminsAsync(supportCase, "approved", actorUserId, cancellationToken);
            await NotifyDriverAccountDriverAsync(supportCase, "approved", cancellationToken);
            return supportCase;
        }

        if (compensationDecision?.Coupon is not null)
        {
            await NotifyCustomerCouponCompensationAsync(
                supportCase.Order,
                supportCase,
                compensationDecision.Coupon,
                approvedAmount!.Value,
                cancellationToken);
        }
        else
        {
            await NotifyCustomerAsync(supportCase.Order, supportCase, "approved", cancellationToken);
        }

        await NotifyVendorSupportCaseAsync(supportCase.Order, supportCase, "approved", cancellationToken);
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
        StagePendingCaseArtifacts(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (supportCase.Order is null)
        {
            await NotifyDriverAccountAdminsAsync(supportCase, "rejected", actorUserId, cancellationToken);
            await NotifyDriverAccountDriverAsync(supportCase, "rejected", cancellationToken);
            return supportCase;
        }

        await NotifyCustomerAsync(supportCase.Order, supportCase, "rejected", cancellationToken);
        await NotifyVendorSupportCaseAsync(supportCase.Order, supportCase, "rejected", cancellationToken);
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
        StagePendingCaseArtifacts(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (supportCase.Order is null)
        {
            await NotifyDriverAccountAdminsAsync(supportCase, "resolved", actorUserId, cancellationToken);
            await NotifyDriverAccountDriverAsync(supportCase, "resolved", cancellationToken);
            return supportCase;
        }

        await NotifyCustomerAsync(supportCase.Order, supportCase, "resolved", cancellationToken);
        await NotifyVendorSupportCaseAsync(supportCase.Order, supportCase, "resolved", cancellationToken);
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
        StagePendingCaseArtifacts(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (supportCase.Order is null)
        {
            await NotifyDriverAccountAdminsAsync(supportCase, "reopened", actorUserId, cancellationToken);
            await NotifyDriverAccountDriverAsync(supportCase, "reopened", cancellationToken);
        }
        else
        {
            await NotifyAdminRecipientsAsync(
                supportCase.Order,
                supportCase,
                "reopened",
                actorUserId,
                notifyEscalatedTeam: false,
                notifyCurrentReviewer: true,
                cancellationToken);

            await NotifyCustomerAsync(supportCase.Order, supportCase, "reopened", cancellationToken);
            await NotifyVendorSupportCaseAsync(supportCase.Order, supportCase, "reopened", cancellationToken);
            await NotifyActiveDriverAsync(supportCase.Order, supportCase, "reopened", cancellationToken);
        }

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
        StagePendingCaseArtifacts(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (supportCase.Order is null)
        {
            await NotifyDriverAccountAdminsAsync(supportCase, "note_added", actorUserId, cancellationToken);
            if (visibleToCustomer)
            {
                await NotifyDriverAccountDriverAsync(supportCase, "note_added", cancellationToken);
            }

            return supportCase;
        }

        if (visibleToCustomer)
        {
            await NotifyCustomerAsync(supportCase.Order, supportCase, "note_added", cancellationToken);
            await NotifyVendorSupportCaseAsync(supportCase.Order, supportCase, "note_added", cancellationToken);
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
        if (supportCase.Order is null)
        {
            throw new BusinessRuleException("VENDOR_RESPONSE_NOT_ALLOWED", "Vendor responses require an order support case.");
        }

        supportCase.AddVendorResponse(vendorUserId, response);
        StagePendingCaseArtifacts(supportCase);
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

        var driverId = await ResolveDriverIdByUserIdAsync(driverUserId, cancellationToken);

        var assignedDriverUserIds = await _context.DeliveryAssignments
            .AsNoTracking()
            .Where(item => item.OrderId == orderId && item.DriverId == driverId)
            .Select(item => item.DriverId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (!assignedDriverUserIds.Contains(driverId))
        {
            throw new ForbiddenAccessException("You are not assigned to this order.");
        }

        supportCase.MergeIntoActiveCase(
            driverUserId,
            "driver",
            response,
            attachments?.Select(item => (item.FileName, item.FileUrl)).ToList());

        StagePendingCaseArtifacts(supportCase);
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

    public async Task<OrderSupportCase> AddDriverAccountAppealMessageAsync(
        Guid caseId,
        Guid driverId,
        Guid driverUserId,
        string response,
        IReadOnlyList<OrderSupportCaseAttachmentInput>? attachments,
        CancellationToken cancellationToken = default)
    {
        var supportCase = await _context.OrderSupportCases
            .Include(item => item.Activities)
            .Include(item => item.Attachments)
            .FirstOrDefaultAsync(item =>
                item.Id == caseId &&
                item.DriverId == driverId &&
                item.CustomerUserId == driverUserId &&
                item.Type == OrderSupportCaseType.DriverAccountAppeal,
                cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", caseId);

        supportCase.MergeIntoActiveCase(
            driverUserId,
            "driver",
            response,
            attachments?.Select(item => (item.FileName, item.FileUrl)).ToList());

        StagePendingCaseArtifacts(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyDriverAccountAdminsAsync(supportCase, "driver_responded", driverUserId, cancellationToken);
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
        StagePendingCaseArtifacts(supportCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (supportCase.Order is null)
        {
            await NotifyDriverAccountAdminsAsync(supportCase, "admin_message", actorUserId, cancellationToken);
            if (AudienceIncludes(audience, "driver"))
            {
                await NotifyDriverAccountDriverAsync(supportCase, "admin_message", cancellationToken);
            }

            return supportCase;
        }

        if (AudienceIncludes(audience, "customer"))
        {
            await NotifyCustomerAsync(supportCase.Order, supportCase, "admin_message", cancellationToken);
        }

        if (AudienceIncludes(audience, "vendor"))
        {
            await NotifyVendorSupportCaseAsync(supportCase.Order, supportCase, "admin_message", cancellationToken);
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

    private async Task<OrderSupportCase?> LoadActiveDriverAccountCaseAsync(Guid driverId, CancellationToken cancellationToken)
    {
        return await _context.OrderSupportCases
            .Include(x => x.Attachments)
            .Include(x => x.Activities)
            .Where(x =>
                x.DriverId == driverId &&
                x.Type == OrderSupportCaseType.DriverAccountAppeal &&
                x.Status != OrderSupportCaseStatus.Rejected &&
                x.Status != OrderSupportCaseStatus.Resolved)
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

    private static bool CanMergeCustomerInitiatedCase(
        OrderSupportCase activeCase,
        OrderSupportCaseType requestedType,
        string initiatorRole)
    {
        return activeCase.Type == requestedType &&
               string.Equals(activeCase.InitiatorRole, NormalizeToken(initiatorRole), StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanMergeAdminInitiatedCase(
        OrderSupportCase activeCase,
        OrderSupportCaseType requestedType)
    {
        return activeCase.Type == requestedType;
    }

    private async Task EnsureRefundDecisionAsync(
        OrderSupportCase supportCase,
        decimal? approvedAmount,
        SupportCaseCompensationDecision compensationDecision,
        string? costBearer,
        string? decisionNotes,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (!approvedAmount.HasValue || approvedAmount.Value <= 0)
        {
            throw new BusinessRuleException("RETURN_REFUND_AMOUNT_REQUIRED", "Return requests require a refund amount when approved.");
        }

        var order = supportCase.Order
            ?? throw new BusinessRuleException("ORDER_REQUIRED_FOR_REFUND", "Return support cases must be linked to an order.");
        var boundedAmount = Math.Min(approvedAmount.Value, order.TotalAmount);

        if (compensationDecision.CompensationType == OrderSupportCaseCompensationType.CouponCompensation)
        {
            order.UpdatePaymentStatus(boundedAmount >= order.TotalAmount
                ? PaymentStatus.Refunded
                : PaymentStatus.PartiallyRefunded);

            if (order.Status != OrderStatus.Refunded)
            {
                order.ChangeStatus(OrderStatus.Refunded, actorUserId, decisionNotes ?? "Support case approved with compensation coupon.");
                _context.OrderStatusHistories.Add(order.StatusHistory.Last());
            }

            await _orderInventoryWorkflowService.ApplyRestockAsync(order.Id, "refund_approved_coupon", cancellationToken);
            return;
        }

        var payment = await _context.Payments
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(item => item.OrderId == order.Id, cancellationToken);

        if (payment is null)
        {
            payment = new Payment(order.Id, order.PaymentMethod, order.TotalAmount);
            payment.MarkAsPaid();
            _context.Payments.Add(payment);
        }

        var refund = await _context.Refunds
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(item => item.OrderSupportCaseId == supportCase.Id, cancellationToken);

        if (refund is null)
        {
            refund = new Refund(payment.Id, boundedAmount, decisionNotes, compensationDecision.RefundMethod, costBearer, supportCase.Id);
            _context.Refunds.Add(refund);
        }
        else
        {
            refund.UpdateDecision(boundedAmount, decisionNotes, compensationDecision.RefundMethod, costBearer, supportCase.Id);
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

        await _orderInventoryWorkflowService.ApplyRestockAsync(order.Id, "refund_approved", cancellationToken);
    }

    private async Task NotifyDriverAccountAdminsAsync(
        OrderSupportCase supportCase,
        string action,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (!supportCase.DriverId.HasValue)
        {
            return;
        }

        var driverName = await _context.Drivers
            .AsNoTracking()
            .Where(driver => driver.Id == supportCase.DriverId.Value)
            .Select(driver => driver.User.FullName)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "Driver";

        var targetUrl = $"/disputes?caseId={supportCase.Id}";
        var titleEn = action == "created" ? "Driver account support case" : "Driver account case updated";
        var bodyEn = $"{driverName} has a driver account support case that needs review.";
        var titleAr = action == "created" ? "حالة دعم لحساب مندوب" : "تحديث على حالة حساب مندوب";
        var bodyAr = $"يوجد طلب دعم لحساب المندوب {driverName} يحتاج إلى مراجعة.";

        if (_adminAlertService is not null)
        {
            await _adminAlertService.SendAsync(
                new AdminAlertRequest(
                    action == "created" ? AdminAlertTypes.DisputeCreated : AdminAlertTypes.DisputeEscalated,
                    AdminAlertCategories.Drivers,
                    ResolveAdminAlertPriority(supportCase.Priority),
                    titleAr,
                    titleEn,
                    bodyAr,
                    bodyEn,
                    supportCase.Id,
                    targetUrl,
                    new
                    {
                        caseId = supportCase.Id,
                        driverId = supportCase.DriverId,
                        action,
                        status = OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Status),
                        type = "driver_account"
                    }),
                cancellationToken);
            return;
        }

        var recipients = await _context.Users
            .AsNoTracking()
            .Where(user =>
                user.Id != actorUserId &&
                user.AccountStatus == AccountStatus.Active &&
                (user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        var data = $"{{\"caseId\":\"{supportCase.Id}\",\"driverId\":\"{supportCase.DriverId}\",\"type\":\"driver_account\",\"action\":\"{action}\",\"targetUrl\":\"{targetUrl}\"}}";
        foreach (var recipientId in recipients)
        {
            await _notificationService.SendToUserAsync(
                recipientId,
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.AdminOrderSupportCaseCreated,
                supportCase.Id,
                data,
                cancellationToken);
        }
    }

    private async Task NotifyDriverAccountDriverAsync(
        OrderSupportCase supportCase,
        string action,
        CancellationToken cancellationToken)
    {
        var titleAr = "تحديث على طلب دعم الحساب";
        var titleEn = "Account support case update";
        var bodyAr = action switch
        {
            "request_evidence" => "فريق الدعم يحتاج معلومات إضافية لمراجعة حسابك.",
            "resolved" => "تم إغلاق طلب دعم حسابك بعد المراجعة.",
            "rejected" => "تم رفض طلب دعم حسابك بعد المراجعة.",
            "approved" => "تمت مراجعة طلب دعم حسابك.",
            _ => "تم تحديث طلب دعم حسابك."
        };
        var bodyEn = action switch
        {
            "request_evidence" => "Support needs more information to review your account.",
            "assigned" => "Support has started reviewing your account support case.",
            "escalated" => "Your account support case was escalated for review.",
            "reopened" => "Your account support case has been reopened.",
            "resolved" => "Your account support case has been resolved.",
            "rejected" => "Your account support case was rejected after review.",
            "approved" => "Your account support case has been reviewed.",
            _ => "Your account support case was updated."
        };
        var data = $"{{\"caseId\":\"{supportCase.Id}\",\"driverId\":\"{supportCase.DriverId}\",\"type\":\"driver_account\",\"action\":\"{action}\"}}";

        await _notificationService.SendToUserAsync(
            supportCase.CustomerUserId,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            NotificationTypes.OrderSupportCaseChanged,
            supportCase.Id,
            data,
            cancellationToken);

        await _notificationService.SendDriverSupportCaseChangedToUserAsync(
            driverUserId: supportCase.CustomerUserId,
            caseId: supportCase.Id,
            driverId: supportCase.DriverId,
            orderId: null,
            orderNumber: null,
            type: OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Type),
            status: OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Status),
            action: action,
            targetUrl: $"/support/cases/{supportCase.Id}",
            cancellationToken: cancellationToken);

        await _notificationService.SendDriverHomeUpdatedAsync(
            supportCase.CustomerUserId,
            cancellationToken);
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

    private async Task NotifyCustomerCouponCompensationAsync(
        Order order,
        OrderSupportCase supportCase,
        Coupon coupon,
        decimal approvedAmount,
        CancellationToken cancellationToken)
    {
        var targetUrl = OrderSupportCaseNotificationComposer.ResolveTargetUrl(order.Id, supportCase.Id);
        var expiresAt = coupon.EndsAtUtc?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");
        var data = $$"""
{"orderId":"{{order.Id}}","caseId":"{{supportCase.Id}}","orderNumber":"{{order.OrderNumber}}","type":"{{OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Type)}}","status":"{{OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Status)}}","action":"approved","targetUrl":"{{targetUrl}}","compensationType":"coupon_compensation","couponCode":"{{coupon.Code}}","couponValue":{{approvedAmount}},"couponExpiresAt":"{{coupon.EndsAtUtc?.ToString("O")}}"}
""";

        const string titleAr = "تمت الموافقة على الاسترجاع ككوبون";
        const string titleEn = "Return approved as coupon";
        var bodyAr = $"تمت الموافقة على طلبك، وتم إصدار كوبون تعويض بقيمة {approvedAmount:0.00} برمز {coupon.Code} صالح حتى {expiresAt}.";
        var bodyEn = $"Your request was approved. A compensation coupon worth {approvedAmount:0.00} was issued with code {coupon.Code}, valid until {expiresAt}.";

        await _notificationService.SendToUserAsync(
            order.UserId,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            "order_support_case",
            supportCase.Id,
            data,
            cancellationToken);

        await _notificationService.SendOrderSupportCaseChangedToUserAsync(
            order.UserId,
            supportCase.Id,
            order.Id,
            order.OrderNumber,
            OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Type),
            OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Status),
            "approved",
            targetUrl,
            cancellationToken);

        await OneSignalMobilePushRequest.CreateHeadsUp(
                order.UserId.ToString(),
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                "order_support_case",
                supportCase.Id,
                data,
                targetUrl)
            .DispatchAsync(_oneSignalPushService, cancellationToken);
    }

    private async Task NotifyVendorSupportCaseAsync(
        Order order,
        OrderSupportCase supportCase,
        string action,
        CancellationToken cancellationToken)
    {
        if (order.Vendor?.UserId is not Guid vendorUserId || vendorUserId == Guid.Empty)
        {
            return;
        }

        const string titleAr = "تحديث في نزاع طلب";
        const string titleEn = "Order support case updated";
        var bodyAr = $"تم تحديث حالة النزاع الخاصة بالطلب #{order.OrderNumber}.";
        var bodyEn = $"The support case for order #{order.OrderNumber} has been updated.";
        var targetUrl = $"/disputes/{supportCase.Id}";
        var data = $$"""
{"action":"{{action}}","orderId":"{{order.Id}}","orderNumber":"{{order.OrderNumber}}","caseId":"{{supportCase.Id}}","type":"{{OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Type)}}","status":"{{OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Status)}}","targetUrl":"{{targetUrl}}"}
""";

        await _notificationService.SendToUserAsync(
            vendorUserId,
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            NotificationTypes.OrderSupportCaseChanged,
            supportCase.Id,
            data,
            cancellationToken);

        await OneSignalMobilePushRequest.CreateHeadsUp(
                vendorUserId.ToString(),
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.OrderSupportCaseChanged,
                supportCase.Id,
                data,
                targetUrl)
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
        var driverId = await _context.DeliveryAssignments
            .AsNoTracking()
            .Where(item => item.OrderId == order.Id && item.DriverId != null)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.DriverId)
            .FirstOrDefaultAsync(cancellationToken);

        if (driverId == null || driverId == Guid.Empty)
        {
            return;
        }

        var driverUserId = await _context.Drivers
            .AsNoTracking()
            .Where(item => item.Id == driverId.Value)
            .Select(item => item.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (driverUserId == Guid.Empty)
        {
            return;
        }

        await NotifyDriverAsync(order, supportCase, driverUserId, action, cancellationToken);
    }

    private async Task<Guid> ResolveDriverIdByUserIdAsync(Guid driverUserId, CancellationToken cancellationToken)
    {
        var driverId = await _context.Drivers
            .AsNoTracking()
            .Where(item => item.UserId == driverUserId)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (driverId == Guid.Empty)
        {
            throw new NotFoundException("Driver", driverUserId);
        }

        return driverId;
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

        var envelope = TryComposeDriverSupportNotification(order, supportCase, action, driverUserId);
        if (envelope is null)
        {
            return;
        }

        await _notificationService.SendToUserAsync(
            driverUserId,
            envelope.Request,
            cancellationToken);

        await _notificationService.SendOrderSupportCaseChangedToUserAsync(
            driverUserId,
            supportCase.Id,
            order.Id,
            order.OrderNumber,
            OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Type),
            OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Status),
            action,
            $"/orders/{order.Id}/cases/{supportCase.Id}",
            cancellationToken);

        await _notificationService.SendDriverSupportCaseChangedToUserAsync(
            driverUserId: driverUserId,
            caseId: supportCase.Id,
            driverId: null,
            orderId: order.Id,
            orderNumber: order.OrderNumber,
            type: OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Type),
            status: OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Status),
            action: action,
            targetUrl: $"/orders/{order.Id}/cases/{supportCase.Id}",
            cancellationToken: cancellationToken);

        await _notificationService.SendDriverHomeUpdatedAsync(
            driverUserId,
            cancellationToken);

        await envelope.PushRequest.DispatchAsync(_oneSignalPushService, cancellationToken);
    }

    private static DriverSupportNotificationEnvelope? TryComposeDriverSupportNotification(
        Order order,
        OrderSupportCase supportCase,
        string action,
        Guid driverUserId)
    {
        var normalizedAction = NormalizeToken(action);
        var type = OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Type);
        var status = OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Status);
        var data = DriverNotificationDataBuilder.Build(
            "support_case_detail",
            $"support.{normalizedAction}",
            orderId: order.Id,
            supportCaseId: supportCase.Id,
            extra: new
            {
                orderNumber = order.OrderNumber,
                type,
                status
            });

        return normalizedAction switch
        {
            "created" => BuildDriverSupportNotification(
                driverUserId,
                "تم إنشاء بلاغ على الطلب",
                "Support case created",
                $"تم إنشاء بلاغ على الطلب رقم #{order.OrderNumber}.",
                $"A support case was created for order #{order.OrderNumber}.",
                NotificationPriorities.Normal,
                OneSignalPushRequestKind.Standard,
                supportCase.Id,
                data),

            "request_evidence" => BuildDriverSupportNotification(
                driverUserId,
                "مطلوب تقديم معلومات إضافية",
                "More information required",
                $"تحتاج قضية الطلب رقم #{order.OrderNumber} إلى رد أو معلومات إضافية منك.",
                $"Order case #{order.OrderNumber} needs a response or additional evidence from you.",
                NotificationPriorities.High,
                OneSignalPushRequestKind.HeadsUp,
                supportCase.Id,
                data),

            "admin_message" => BuildDriverSupportNotification(
                driverUserId,
                "رسالة جديدة بخصوص الطلب",
                "New support message",
                $"توجد رسالة دعم جديدة بخصوص الطلب رقم #{order.OrderNumber}.",
                $"There is a new support message about order #{order.OrderNumber}.",
                NotificationPriorities.High,
                OneSignalPushRequestKind.HeadsUp,
                supportCase.Id,
                data),

            "assigned" => BuildDriverSupportNotification(
                driverUserId,
                "بدأت مراجعة البلاغ",
                "Support case under review",
                $"بدأ فريق الدعم مراجعة بلاغ الطلب رقم #{order.OrderNumber}.",
                $"Support has started reviewing the case for order #{order.OrderNumber}.",
                NotificationPriorities.Normal,
                OneSignalPushRequestKind.Standard,
                supportCase.Id,
                data),

            "escalated" => BuildDriverSupportNotification(
                driverUserId,
                "تم تصعيد البلاغ",
                "Support case escalated",
                $"تم تصعيد بلاغ الطلب رقم #{order.OrderNumber} للمراجعة.",
                $"The support case for order #{order.OrderNumber} was escalated for review.",
                NotificationPriorities.High,
                OneSignalPushRequestKind.HeadsUp,
                supportCase.Id,
                data),

            "approved" => BuildDriverSupportNotification(
                driverUserId,
                "تمت الموافقة على البلاغ",
                "Support case approved",
                $"تمت الموافقة على قرار البلاغ الخاص بالطلب رقم #{order.OrderNumber}.",
                $"The support case decision for order #{order.OrderNumber} was approved.",
                NotificationPriorities.High,
                OneSignalPushRequestKind.Standard,
                supportCase.Id,
                data),

            "rejected" => BuildDriverSupportNotification(
                driverUserId,
                "تم رفض البلاغ",
                "Support case rejected",
                $"تم رفض البلاغ الخاص بالطلب رقم #{order.OrderNumber}.",
                $"The support case for order #{order.OrderNumber} was rejected.",
                NotificationPriorities.High,
                OneSignalPushRequestKind.Standard,
                supportCase.Id,
                data),

            "resolved" => BuildDriverSupportNotification(
                driverUserId,
                "تم إغلاق البلاغ",
                "Support case resolved",
                $"تم إغلاق البلاغ الخاص بالطلب رقم #{order.OrderNumber}.",
                $"The support case for order #{order.OrderNumber} was resolved.",
                NotificationPriorities.Normal,
                OneSignalPushRequestKind.Standard,
                supportCase.Id,
                data),

            "reopened" => BuildDriverSupportNotification(
                driverUserId,
                "تم إعادة فتح البلاغ",
                "Support case reopened",
                $"تم إعادة فتح بلاغ الطلب رقم #{order.OrderNumber}.",
                $"The support case for order #{order.OrderNumber} has been reopened.",
                NotificationPriorities.Normal,
                OneSignalPushRequestKind.Standard,
                supportCase.Id,
                data),

            _ => null
        };
    }

    private static DriverSupportNotificationEnvelope BuildDriverSupportNotification(
        Guid driverUserId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string priority,
        OneSignalPushRequestKind pushKind,
        Guid referenceId,
        string data)
    {
        var request = new NotificationDispatchRequest(
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            NotificationTypes.OrderSupportCaseChanged,
            NotificationCategories.Support,
            priority,
            referenceId,
            data);

        var pushRequest = pushKind == OneSignalPushRequestKind.HeadsUp
            ? OneSignalMobilePushRequest.CreateHeadsUp(
                driverUserId.ToString(),
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.OrderSupportCaseChanged,
                referenceId,
                data,
                category: NotificationCategories.Support,
                targetApplication: OneSignalApplicationTarget.Driver)
            : OneSignalMobilePushRequest.CreateStandard(
                driverUserId.ToString(),
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                NotificationTypes.OrderSupportCaseChanged,
                referenceId,
                data,
                category: NotificationCategories.Support,
                targetApplication: OneSignalApplicationTarget.Driver);

        return new DriverSupportNotificationEnvelope(request, pushRequest);
    }

    private sealed record DriverSupportNotificationEnvelope(
        NotificationDispatchRequest Request,
        OneSignalMobilePushRequest PushRequest);

    private enum OneSignalPushRequestKind
    {
        Standard,
        HeadsUp
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
        if (_adminAlertService is not null)
        {
            var composedAlert = OrderSupportCaseNotificationComposer.ComposeAdmin(
                order.Id,
                supportCase.Id,
                order.OrderNumber,
                supportCase.Type,
                supportCase.Status,
                supportCase.Queue,
                supportCase.Priority,
                action);

            await _adminAlertService.SendAsync(
                new AdminAlertRequest(
                    ResolveAdminAlertType(supportCase, action),
                    ResolveAdminAlertCategory(supportCase),
                    ResolveAdminAlertPriority(supportCase.Priority),
                    composedAlert.TitleAr,
                    composedAlert.TitleEn,
                    composedAlert.BodyAr,
                    composedAlert.BodyEn,
                    supportCase.Id,
                    composedAlert.TargetUrl,
                    new
                    {
                        orderId = order.Id,
                        caseId = supportCase.Id,
                        orderNumber = order.OrderNumber,
                        action,
                        status = OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Status),
                        type = OrderSupportCaseNotificationComposer.ToApiValue(supportCase.Type)
                    }),
                cancellationToken);
            return;
        }

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

    private static string ResolveAdminAlertType(OrderSupportCase supportCase, string action)
    {
        if (supportCase.Priority == OrderSupportCasePriority.Critical && action == "created")
        {
            return AdminAlertTypes.SupportCriticalCreated;
        }

        if (action == "created")
        {
            return supportCase.Type == OrderSupportCaseType.ReturnRequest
                ? AdminAlertTypes.RefundRequested
                : AdminAlertTypes.DisputeCreated;
        }

        return AdminAlertTypes.DisputeEscalated;
    }

    private static string ResolveAdminAlertCategory(OrderSupportCase supportCase) =>
        supportCase.Type == OrderSupportCaseType.ReturnRequest
            ? AdminAlertCategories.Refunds
            : AdminAlertCategories.Disputes;

    private static string ResolveAdminAlertPriority(OrderSupportCasePriority priority) =>
        priority switch
        {
            OrderSupportCasePriority.Critical => AdminAlertPriorities.Critical,
            OrderSupportCasePriority.High => AdminAlertPriorities.High,
            OrderSupportCasePriority.Low => AdminAlertPriorities.Low,
            _ => AdminAlertPriorities.Normal
        };

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
            ?? supportCase.Order?.TotalAmount;
    }

    private async Task<SupportCaseCompensationDecision> ResolveCompensationDecisionAsync(
        OrderSupportCase supportCase,
        decimal? approvedAmount,
        string? refundMethod,
        CancellationToken cancellationToken)
    {
        if (!approvedAmount.HasValue || approvedAmount.Value <= 0)
        {
            throw new BusinessRuleException("RETURN_REFUND_AMOUNT_REQUIRED", "Return requests require a refund amount when approved.");
        }

        var order = supportCase.Order
            ?? throw new BusinessRuleException("ORDER_REQUIRED_FOR_REFUND", "Return support cases must be linked to an order.");

        return order.PaymentMethod == PaymentMethodType.CashOnDelivery
            ? await ResolveCodCompensationDecisionAsync(supportCase, approvedAmount.Value, refundMethod, cancellationToken)
            : ResolveOnlineCompensationDecision(refundMethod);
    }

    private async Task<SupportCaseCompensationDecision> ResolveCodCompensationDecisionAsync(
        OrderSupportCase supportCase,
        decimal approvedAmount,
        string? refundMethod,
        CancellationToken cancellationToken)
    {
        var normalizedMethod = NormalizeToken(refundMethod) ?? "coupon";
        if (normalizedMethod != "coupon")
        {
            throw new BusinessRuleException("INVALID_RETURN_COMPENSATION_METHOD", "Cash on delivery return requests can only be approved as coupon compensation.");
        }

        var coupon = await CreateCompensationCouponAsync(supportCase, approvedAmount, cancellationToken);
        return new SupportCaseCompensationDecision("coupon", OrderSupportCaseCompensationType.CouponCompensation, coupon);
    }

    private static SupportCaseCompensationDecision ResolveOnlineCompensationDecision(string? refundMethod)
    {
        var normalizedMethod = NormalizeToken(refundMethod) ?? "same_method";
        if (normalizedMethod != "same_method")
        {
            throw new BusinessRuleException("INVALID_RETURN_COMPENSATION_METHOD", "Online-paid return requests can only be approved to the original payment method.");
        }

        return new SupportCaseCompensationDecision("same_method", OrderSupportCaseCompensationType.CashRefund, null);
    }

    private async Task<Coupon> CreateCompensationCouponAsync(
        OrderSupportCase supportCase,
        decimal approvedAmount,
        CancellationToken cancellationToken)
    {
        if (supportCase.CompensationCouponId.HasValue)
        {
            var existingCoupon = await _context.Coupons
                .FirstOrDefaultAsync(item => item.Id == supportCase.CompensationCouponId.Value, cancellationToken);

            if (existingCoupon is not null)
            {
                return existingCoupon;
            }
        }

        var order = supportCase.Order
            ?? throw new BusinessRuleException("ORDER_REQUIRED_FOR_REFUND", "Return support cases must be linked to an order.");
        var code = await GenerateUniqueCompensationCouponCodeAsync(cancellationToken);
        var coupon = new Coupon(
            code,
            $"Support compensation for order {order.OrderNumber}",
            CouponDiscountType.Fixed,
            approvedAmount,
            minOrderAmount: null,
            maxDiscountAmount: approvedAmount,
            startsAtUtc: DateTime.UtcNow,
            endsAtUtc: DateTime.UtcNow.AddDays(30),
            usageLimit: 1,
            perUserLimit: 1,
            assignedUserId: order.UserId,
            sourceType: CouponSourceType.SupportCompensation,
            orderSupportCaseId: supportCase.Id);

        _context.Coupons.Add(coupon);
        return coupon;
    }

    private async Task<string> GenerateUniqueCompensationCouponCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var code = $"RET-{suffix}";
            var exists = await _context.Coupons
                .AsNoTracking()
                .AnyAsync(item => item.Code == code, cancellationToken);

            if (!exists)
            {
                return code;
            }
        }

        throw new BusinessRuleException("COUPON_CODE_GENERATION_FAILED", "Unable to generate a unique compensation coupon code.");
    }

    private static OrderSupportCaseType ParseType(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "return_request" or "return" or "refund" => OrderSupportCaseType.ReturnRequest,
            "complaint" or "issue" => OrderSupportCaseType.Complaint,
            "driver_report" => OrderSupportCaseType.DriverReport,
            "driver_dispute" or "dispute" => OrderSupportCaseType.DriverDispute,
            "driver_account" or "driver_account_appeal" => OrderSupportCaseType.DriverAccountAppeal,
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

        if (type == OrderSupportCaseType.DriverAccountAppeal)
        {
            return normalizedReason is "login_locked" or "account_banned"
                ? OrderSupportCasePriority.High
                : OrderSupportCasePriority.Medium;
        }

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

        if (type == OrderSupportCaseType.DriverAccountAppeal)
        {
            return OrderSupportCaseQueue.DriverOps;
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

        StagePendingCaseArtifacts(supportCase);
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

    private void StagePendingCaseArtifacts(OrderSupportCase supportCase)
    {
        if (_context is not DbContext dbContext)
        {
            return;
        }

        foreach (var activity in supportCase.Activities.Where(item => item.CreatedAtUtc == default))
        {
            var entry = dbContext.Entry(activity);
            if (entry.State != EntityState.Added)
            {
                entry.State = EntityState.Added;
            }
        }

        foreach (var attachment in supportCase.Attachments.Where(item => item.CreatedAtUtc == default))
        {
            var entry = dbContext.Entry(attachment);
            if (entry.State != EntityState.Added)
            {
                entry.State = EntityState.Added;
            }
        }
    }

    private sealed record SupportCaseCompensationDecision(
        string RefundMethod,
        OrderSupportCaseCompensationType CompensationType,
        Coupon? Coupon);
}
