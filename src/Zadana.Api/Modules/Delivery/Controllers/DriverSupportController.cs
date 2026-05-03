using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/drivers/support")]
[Tags("Driver App API")]
[Authorize(Policy = "DriverOnly")]
public class DriverSupportController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDriverRepository _driverRepository;
    private readonly IOrderSupportCaseWorkflowService _workflowService;

    public DriverSupportController(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IDriverRepository driverRepository,
        IOrderSupportCaseWorkflowService workflowService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _driverRepository = driverRepository;
        _workflowService = workflowService;
    }

    /// <summary>
    /// Driver reports an issue on an active order (wrong address, customer unavailable, damaged package, etc.)
    /// </summary>
    [HttpPost("orders/{orderId:guid}/report-issue")]
    public async Task<ActionResult<DriverSupportCaseResponse>> ReportIssue(
        Guid orderId,
        [FromBody] DriverReportIssueRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Message is required.");
        }

        var (driverId, userId) = await ResolveDriverAsync(cancellationToken);

        // Ensure the driver has an active assignment for this order
        var hasAssignment = await _dbContext.DeliveryAssignments
            .AnyAsync(a => a.DriverId == driverId && a.Order.Id == orderId, cancellationToken);

        if (!hasAssignment)
        {
            throw new BusinessRuleException("NOT_ASSIGNED_TO_ORDER", "You can only report issues for orders assigned to you.");
        }

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        var supportCase = await _workflowService.CreateCustomerCaseAsync(
            orderId,
            userId,
            "driver_report",
            request.ReasonCode,
            request.Message,
            request.Attachments?.Select(a => new OrderSupportCaseAttachmentInput(a.FileName, a.FileUrl)).ToList(),
            cancellationToken);

        return Ok(new DriverSupportCaseResponse(
            supportCase.Id,
            supportCase.OrderId,
            order.OrderNumber,
            ToApiType(supportCase.Type),
            GetTypeLabelAr(supportCase.Type),
            GetTypeLabelEn(supportCase.Type),
            ToApiStatus(supportCase.Status),
            GetStatusLabelAr(supportCase.Status),
            GetStatusLabelEn(supportCase.Status),
            ToApiPriority(supportCase.Priority),
            GetPriorityLabelAr(supportCase.Priority),
            GetPriorityLabelEn(supportCase.Priority),
            supportCase.ReasonCode,
            GetReasonLabelAr(supportCase.Type, supportCase.ReasonCode),
            GetReasonLabelEn(supportCase.Type, supportCase.ReasonCode),
            supportCase.Message,
            supportCase.CreatedAtUtc));
    }

    /// <summary>
    /// Driver raises a financial dispute (payout issue, incorrect deduction, etc.)
    /// </summary>
    [HttpPost("orders/{orderId:guid}/dispute")]
    public async Task<ActionResult<DriverSupportCaseResponse>> RaiseDispute(
        Guid orderId,
        [FromBody] DriverDisputeRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Message is required.");
        }

        var (driverId, userId) = await ResolveDriverAsync(cancellationToken);

        var hasAssignment = await _dbContext.DeliveryAssignments
            .AnyAsync(a => a.DriverId == driverId && a.Order.Id == orderId, cancellationToken);

        if (!hasAssignment)
        {
            throw new BusinessRuleException("NOT_ASSIGNED_TO_ORDER", "You can only dispute orders assigned to you.");
        }

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        var supportCase = await _workflowService.CreateCustomerCaseAsync(
            orderId,
            userId,
            "driver_dispute",
            request.ReasonCode ?? "payout_dispute",
            request.Message,
            null,
            cancellationToken);

        return Ok(new DriverSupportCaseResponse(
            supportCase.Id,
            supportCase.OrderId,
            order.OrderNumber,
            ToApiType(supportCase.Type),
            GetTypeLabelAr(supportCase.Type),
            GetTypeLabelEn(supportCase.Type),
            ToApiStatus(supportCase.Status),
            GetStatusLabelAr(supportCase.Status),
            GetStatusLabelEn(supportCase.Status),
            ToApiPriority(supportCase.Priority),
            GetPriorityLabelAr(supportCase.Priority),
            GetPriorityLabelEn(supportCase.Priority),
            supportCase.ReasonCode,
            GetReasonLabelAr(supportCase.Type, supportCase.ReasonCode),
            GetReasonLabelEn(supportCase.Type, supportCase.ReasonCode),
            supportCase.Message,
            supportCase.CreatedAtUtc));
    }

    /// <summary>
    /// Get all support cases opened by this driver.
    /// </summary>
    [HttpGet("cases")]
    public async Task<ActionResult<DriverSupportCasesListResponse>> GetMyCases(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (_, userId) = await ResolveDriverAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _dbContext.OrderSupportCases
            .AsNoTracking()
            .Include(c => c.Order)
            .Where(c => c.CustomerUserId == userId &&
                        (c.Type == OrderSupportCaseType.DriverReport || c.Type == OrderSupportCaseType.DriverDispute))
            .OrderByDescending(c => c.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var cases = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = cases
            .Select(c => new DriverSupportCaseListItemResponse(
                c.Id,
                c.OrderId,
                c.Order.OrderNumber,
                ToApiType(c.Type),
                GetTypeLabelAr(c.Type),
                GetTypeLabelEn(c.Type),
                ToApiStatus(c.Status),
                GetStatusLabelAr(c.Status),
                GetStatusLabelEn(c.Status),
                ToApiPriority(c.Priority),
                GetPriorityLabelAr(c.Priority),
                GetPriorityLabelEn(c.Priority),
                c.ReasonCode,
                GetReasonLabelAr(c.Type, c.ReasonCode),
                GetReasonLabelEn(c.Type, c.ReasonCode),
                c.Message,
                c.CustomerVisibleNote,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                c.ClosedAtUtc))
            .ToList();

        return Ok(new DriverSupportCasesListResponse(items, page, pageSize, total));
    }

    /// <summary>
    /// Get details of a specific support case.
    /// </summary>
    [HttpGet("cases/{caseId:guid}")]
    public async Task<ActionResult<DriverSupportCaseDetailResponse>> GetCaseDetail(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var (_, userId) = await ResolveDriverAsync(cancellationToken);

        var supportCase = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Include(c => c.Order)
            .Include(c => c.Attachments)
            .Include(c => c.Activities)
            .Where(c => c.Id == caseId && c.CustomerUserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", caseId);

        var activities = supportCase.Activities
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new DriverSupportCaseActivityResponse(
                ToApiAction(a.Action),
                GetActionLabelAr(a.Action),
                GetActionLabelEn(a.Action),
                a.Title,
                GetActivityTitleAr(a),
                GetActivityTitleEn(a),
                a.Note,
                NormalizeRole(a.ActorRole),
                GetRoleLabelAr(a.ActorRole),
                GetRoleLabelEn(a.ActorRole),
                a.CreatedAtUtc))
            .ToList();

        var attachments = supportCase.Attachments
            .Select(a => new DriverSupportCaseAttachmentResponse(a.FileName, a.FileUrl))
            .ToList();

        return Ok(new DriverSupportCaseDetailResponse(
            supportCase.Id,
            supportCase.OrderId,
            supportCase.Order.OrderNumber,
            ToApiType(supportCase.Type),
            GetTypeLabelAr(supportCase.Type),
            GetTypeLabelEn(supportCase.Type),
            ToApiStatus(supportCase.Status),
            GetStatusLabelAr(supportCase.Status),
            GetStatusLabelEn(supportCase.Status),
            ToApiPriority(supportCase.Priority),
            GetPriorityLabelAr(supportCase.Priority),
            GetPriorityLabelEn(supportCase.Priority),
            ToApiQueue(supportCase.Queue),
            GetQueueLabelAr(supportCase.Queue),
            GetQueueLabelEn(supportCase.Queue),
            supportCase.ReasonCode,
            GetReasonLabelAr(supportCase.Type, supportCase.ReasonCode),
            GetReasonLabelEn(supportCase.Type, supportCase.ReasonCode),
            supportCase.Message,
            supportCase.CustomerVisibleNote,
            supportCase.DecisionNotes,
            supportCase.CreatedAtUtc,
            supportCase.UpdatedAtUtc,
            supportCase.ClosedAtUtc,
            attachments,
            activities));
    }

    [HttpPost("orders/{orderId:guid}/cases/{caseId:guid}/messages")]
    public async Task<ActionResult<DriverSupportCaseResponse>> SendMessage(
        Guid orderId,
        Guid caseId,
        [FromBody] DriverReportIssueRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Message is required.");
        }

        var (_, userId) = await ResolveDriverAsync(cancellationToken);

        var supportCase = await _workflowService.AddDriverResponseAsync(
            caseId,
            orderId,
            userId,
            request.Message,
            request.Attachments?.Select(a => new OrderSupportCaseAttachmentInput(a.FileName, a.FileUrl)).ToList(),
            cancellationToken);

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);

        return Ok(new DriverSupportCaseResponse(
            supportCase.Id,
            supportCase.OrderId,
            order.OrderNumber,
            ToApiType(supportCase.Type),
            GetTypeLabelAr(supportCase.Type),
            GetTypeLabelEn(supportCase.Type),
            ToApiStatus(supportCase.Status),
            GetStatusLabelAr(supportCase.Status),
            GetStatusLabelEn(supportCase.Status),
            ToApiPriority(supportCase.Priority),
            GetPriorityLabelAr(supportCase.Priority),
            GetPriorityLabelEn(supportCase.Priority),
            supportCase.ReasonCode,
            GetReasonLabelAr(supportCase.Type, supportCase.ReasonCode),
            GetReasonLabelEn(supportCase.Type, supportCase.ReasonCode),
            supportCase.Message,
            supportCase.CreatedAtUtc));
    }

    [HttpGet("reasons/{type}")]
    [AllowAnonymous]
    public ActionResult<IReadOnlyList<DriverSupportReasonResponse>> GetSupportReasons(string type)
    {
        var reasons = OrderSupportCaseReasonCatalog.GetReasonsByType(type);

        var response = reasons.Select(r => new DriverSupportReasonResponse(
            r.Code,
            r.LabelAr,
            r.LabelEn,
            r.RequiresNote)).ToList();

        return Ok(response);
    }

    private async Task<(Guid DriverId, Guid UserId)> ResolveDriverAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");

        var driver = await _driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return (driver.Id, userId);
    }

    private static string ToApiType(OrderSupportCaseType type) =>
        type switch
        {
            OrderSupportCaseType.ReturnRequest => "return_request",
            OrderSupportCaseType.DriverReport => "driver_report",
            OrderSupportCaseType.DriverDispute => "driver_dispute",
            _ => "complaint"
        };

    private static string ToApiStatus(OrderSupportCaseStatus status) =>
        status switch
        {
            OrderSupportCaseStatus.InReview => "in_review",
            OrderSupportCaseStatus.AwaitingCustomerEvidence => "awaiting_customer_evidence",
            _ => status.ToString().ToLowerInvariant()
        };

    private static string ToApiPriority(OrderSupportCasePriority priority) =>
        priority.ToString().ToLowerInvariant();

    private static string ToApiQueue(OrderSupportCaseQueue queue) =>
        queue switch
        {
            OrderSupportCaseQueue.DriverOps => "driver_ops",
            _ => queue.ToString().ToLowerInvariant()
        };

    private static string ToApiAction(string action) =>
        string.IsNullOrWhiteSpace(action) ? "unknown" : action.Trim().ToLowerInvariant();

    private static string NormalizeRole(string role) =>
        string.IsNullOrWhiteSpace(role) ? "unknown" : role.Trim().ToLowerInvariant();

    private static string GetTypeLabelAr(OrderSupportCaseType type) => type switch
    {
        OrderSupportCaseType.DriverReport => "بلاغ تشغيلي",
        OrderSupportCaseType.DriverDispute => "نزاع مالي",
        OrderSupportCaseType.ReturnRequest => "طلب استرجاع",
        _ => "شكوى"
    };

    private static string GetTypeLabelEn(OrderSupportCaseType type) => type switch
    {
        OrderSupportCaseType.DriverReport => "Operational report",
        OrderSupportCaseType.DriverDispute => "Financial dispute",
        OrderSupportCaseType.ReturnRequest => "Return request",
        _ => "Complaint"
    };

    private static string GetStatusLabelAr(OrderSupportCaseStatus status) => status switch
    {
        OrderSupportCaseStatus.Submitted => "تم الاستلام",
        OrderSupportCaseStatus.InReview => "قيد المراجعة",
        OrderSupportCaseStatus.AwaitingCustomerEvidence => "بانتظار معلومات إضافية",
        OrderSupportCaseStatus.Approved => "تمت الموافقة",
        OrderSupportCaseStatus.Rejected => "تم الرفض",
        _ => "تم الحل"
    };

    private static string GetStatusLabelEn(OrderSupportCaseStatus status) => status switch
    {
        OrderSupportCaseStatus.Submitted => "Submitted",
        OrderSupportCaseStatus.InReview => "In review",
        OrderSupportCaseStatus.AwaitingCustomerEvidence => "Awaiting more evidence",
        OrderSupportCaseStatus.Approved => "Approved",
        OrderSupportCaseStatus.Rejected => "Rejected",
        _ => "Resolved"
    };

    private static string GetPriorityLabelAr(OrderSupportCasePriority priority) => priority switch
    {
        OrderSupportCasePriority.Low => "منخفضة",
        OrderSupportCasePriority.Medium => "متوسطة",
        OrderSupportCasePriority.High => "مرتفعة",
        _ => "حرجة"
    };

    private static string GetPriorityLabelEn(OrderSupportCasePriority priority) => priority switch
    {
        OrderSupportCasePriority.Low => "Low",
        OrderSupportCasePriority.Medium => "Medium",
        OrderSupportCasePriority.High => "High",
        _ => "Critical"
    };

    private static string GetQueueLabelAr(OrderSupportCaseQueue queue) => queue switch
    {
        OrderSupportCaseQueue.Support => "الدعم",
        OrderSupportCaseQueue.Finance => "المالية",
        OrderSupportCaseQueue.Operations => "العمليات",
        OrderSupportCaseQueue.Risk => "المخاطر",
        OrderSupportCaseQueue.Legal => "الشؤون القانونية",
        _ => "عمليات المندوبين"
    };

    private static string GetQueueLabelEn(OrderSupportCaseQueue queue) => queue switch
    {
        OrderSupportCaseQueue.Support => "Support",
        OrderSupportCaseQueue.Finance => "Finance",
        OrderSupportCaseQueue.Operations => "Operations",
        OrderSupportCaseQueue.Risk => "Risk",
        OrderSupportCaseQueue.Legal => "Legal",
        _ => "Driver operations"
    };

    private static string? GetReasonLabelAr(OrderSupportCaseType type, string? reasonCode) =>
        FindReason(type, reasonCode)?.LabelAr;

    private static string? GetReasonLabelEn(OrderSupportCaseType type, string? reasonCode) =>
        FindReason(type, reasonCode)?.LabelEn;

    private static OrderSupportCaseReasonOption? FindReason(OrderSupportCaseType type, string? reasonCode) =>
        OrderSupportCaseReasonCatalog.FindReason(ToApiType(type), reasonCode);

    private static string GetRoleLabelAr(string role) => NormalizeRole(role) switch
    {
        "admin" => "الإدارة",
        "vendor" => "التاجر",
        "customer" => "العميل",
        "driver" => "المندوب",
        _ => "النظام"
    };

    private static string GetRoleLabelEn(string role) => NormalizeRole(role) switch
    {
        "admin" => "Admin",
        "vendor" => "Vendor",
        "customer" => "Customer",
        "driver" => "Driver",
        _ => "System"
    };

    private static string GetActionLabelAr(string action) => ToApiAction(action) switch
    {
        "submitted" => "تم فتح القضية",
        "driver_response" => "رد المندوب",
        "vendor_response" => "رد التاجر",
        "customer_response" => "رد العميل",
        "request_evidence" => "طلب معلومات إضافية",
        "assigned" => "تم الإسناد",
        "escalated" => "تم التصعيد",
        "approved" => "تمت الموافقة",
        "rejected" => "تم الرفض",
        "resolved" => "تم الحل",
        "reopened" => "أعيد فتح القضية",
        "admin_message" => "تحديث من الإدارة",
        "internal_note" => "ملاحظة داخلية",
        "customer_note" => "ملاحظة للعميل",
        _ => "تحديث على القضية"
    };

    private static string GetActionLabelEn(string action) => ToApiAction(action) switch
    {
        "submitted" => "Case opened",
        "driver_response" => "Driver replied",
        "vendor_response" => "Vendor replied",
        "customer_response" => "Customer replied",
        "request_evidence" => "Evidence requested",
        "assigned" => "Assigned",
        "escalated" => "Escalated",
        "approved" => "Approved",
        "rejected" => "Rejected",
        "resolved" => "Resolved",
        "reopened" => "Reopened",
        "admin_message" => "Admin update",
        "internal_note" => "Internal note",
        "customer_note" => "Customer note",
        _ => "Case updated"
    };

    private static string GetActivityTitleAr(OrderSupportCaseActivity activity) => ToApiAction(activity.Action) switch
    {
        "submitted" => activity.ActorRole.Equals("driver", StringComparison.OrdinalIgnoreCase)
            ? "تم إنشاء بلاغ من المندوب"
            : "تم إنشاء قضية جديدة",
        "driver_response" => "أرسل المندوب ردًا جديدًا",
        "vendor_response" => "أرسل التاجر ردًا جديدًا",
        "customer_response" => "أرسل العميل ردًا جديدًا",
        "request_evidence" => "طلبت الإدارة معلومات إضافية",
        "assigned" => "تم إسناد القضية للمراجعة",
        "escalated" => "تم تصعيد القضية إلى فريق مختص",
        "approved" => "تمت الموافقة على القضية",
        "rejected" => "تم رفض القضية",
        "resolved" => "تم إغلاق القضية بعد الحل",
        "reopened" => "أعيد فتح القضية",
        "admin_message" => "أرسلت الإدارة تحديثًا جديدًا",
        "internal_note" => "أضيفت ملاحظة داخلية",
        "customer_note" => "أضيفت ملاحظة ظاهرة للأطراف",
        _ => activity.Title
    };

    private static string GetActivityTitleEn(OrderSupportCaseActivity activity) => ToApiAction(activity.Action) switch
    {
        "submitted" => activity.ActorRole.Equals("driver", StringComparison.OrdinalIgnoreCase)
            ? "Driver created a new report"
            : "A new case was created",
        "driver_response" => "Driver sent a new reply",
        "vendor_response" => "Vendor sent a new reply",
        "customer_response" => "Customer sent a new reply",
        "request_evidence" => "Admin requested more information",
        "assigned" => "Case assigned for review",
        "escalated" => "Case escalated to a specialized queue",
        "approved" => "Case approved",
        "rejected" => "Case rejected",
        "resolved" => "Case resolved and closed",
        "reopened" => "Case reopened",
        "admin_message" => "Admin shared a new update",
        "internal_note" => "Internal note added",
        "customer_note" => "Public note added",
        _ => activity.Title
    };
}
