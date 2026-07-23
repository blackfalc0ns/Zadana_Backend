using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Common.Export;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Export;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Orders.Controllers;

[Route("api/admin/order-cases")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin Dashboard API")]
public class AdminOrderCasesController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IOrderReadService _orderReadService;
    private readonly IOrderSupportCaseWorkflowService _orderSupportCaseWorkflowService;
    private readonly IApplicationDbContext _dbContext;

    public AdminOrderCasesController(
        ICurrentUserService currentUserService,
        IOrderReadService orderReadService,
        IOrderSupportCaseWorkflowService orderSupportCaseWorkflowService,
        IApplicationDbContext dbContext)
    {
        _currentUserService = currentUserService;
        _orderReadService = orderReadService;
        _orderSupportCaseWorkflowService = orderSupportCaseWorkflowService;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<AdminOrderSupportCasesListDto>> GetCases(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? queue,
        [FromQuery] string? initiatorRole,
        [FromQuery] Guid? vendorId,
        [FromQuery] Guid? driverId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _orderReadService.GetAdminOrderSupportCasesAsync(
            search,
            type,
            status,
            priority,
            queue,
            initiatorRole,
            vendorId,
            driverId,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportCases(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? queue,
        [FromQuery] string? initiatorRole,
        [FromQuery] Guid? vendorId,
        [FromQuery] Guid? driverId,
        CancellationToken cancellationToken = default)
    {
        var result = await _orderReadService.GetAdminOrderSupportCasesAsync(
            search,
            type,
            status,
            priority,
            queue,
            initiatorRole,
            vendorId,
            driverId,
            1,
            ExportLimits.MaxRows,
            cancellationToken);

        var file = ExcelExportBuilder.BuildFromObjects(
            ExportFileResult.StampFileName("order-cases", ".xlsx"),
            ExportText.Label("Order Cases", "حالات الطلبات"),
            [
                ExportText.Column("ID", "المعرّف", "id"),
                ExportText.Column("Order ID", "معرّف الطلب", "orderId"),
                ExportText.Column("Order Display ID", "رقم الطلب", "orderDisplayId"),
                ExportText.Column("Customer Name", "اسم العميل", "customerName"),
                ExportText.Column("Customer Email", "بريد العميل", "customerEmail"),
                ExportText.Column("Merchant Name", "اسم التاجر", "merchantName"),
                ExportText.Column("Type", "النوع", "type"),
                ExportText.Column("Type Label", "تسمية النوع", "typeLabel"),
                ExportText.Column("Reason Code", "رمز السبب", "reasonCode"),
                ExportText.Column("Reason", "السبب", "reason"),
                ExportText.Column("Amount", "المبلغ", "amount"),
                ExportText.Column("Case Status", "حالة القضية", "caseStatus"),
                ExportText.Column("Case Status Label", "تسمية حالة القضية", "caseStatusLabel"),
                ExportText.Column("Status", "الحالة", "status"),
                ExportText.Column("Status Label", "تسمية الحالة", "statusLabel"),
                ExportText.Column("Priority", "الأولوية", "priority"),
                ExportText.Column("Priority Label", "تسمية الأولوية", "priorityLabel"),
                ExportText.Column("Owner", "المسؤول", "owner"),
                ExportText.Column("Queue", "الطابور", "queue"),
                ExportText.Column("Queue Label", "تسمية الطابور", "queueLabel"),
                ExportText.Column("Risk", "المخاطر", "risk"),
                ExportText.Column("Created At", "تاريخ الإنشاء", "createdAt"),
                ExportText.Column("SLA", "اتفاقية الخدمة", "sla"),
                ExportText.Column("Note", "ملاحظة", "note"),
                ExportText.Column("Payment Method", "طريقة الدفع", "paymentMethod"),
                ExportText.Column("Payment Mask", "قناع الدفع", "paymentMask"),
                ExportText.Column("Initiator Role", "دور البادئ", "initiatorRole"),
                ExportText.Column("Waiting On Role", "بانتظار دور", "waitingOnRole"),
                ExportText.Column("Settlement Status", "حالة التسوية", "settlementStatus"),
                ExportText.Column("Vendor Recovery Status", "حالة استرداد التاجر", "vendorRecoveryStatus"),
                ExportText.Column("Vendor Recovered", "مسترد من التاجر", "vendorRecoveredAmount"),
                ExportText.Column("Vendor Outstanding", "مستحق على التاجر", "vendorOutstandingAmount")
            ],
            result.Items,
            item => new Dictionary<string, string?>
            {
                ["id"] = item.Id.ToString(),
                ["orderId"] = item.OrderId?.ToString(),
                ["orderDisplayId"] = item.OrderDisplayId,
                ["customerName"] = item.CustomerName,
                ["customerEmail"] = item.CustomerEmail,
                ["merchantName"] = item.MerchantName,
                ["type"] = item.Type,
                ["typeLabel"] = item.TypeLabel,
                ["reasonCode"] = item.ReasonCode,
                ["reason"] = item.Reason,
                ["amount"] = item.Amount.ToString("0.##"),
                ["caseStatus"] = item.CaseStatus,
                ["caseStatusLabel"] = item.CaseStatusLabel,
                ["status"] = item.Status,
                ["statusLabel"] = item.StatusLabel,
                ["priority"] = item.Priority,
                ["priorityLabel"] = item.PriorityLabel,
                ["owner"] = item.Owner,
                ["queue"] = item.Queue,
                ["queueLabel"] = item.QueueLabel,
                ["risk"] = item.Risk,
                ["createdAt"] = item.CreatedAt,
                ["sla"] = item.Sla,
                ["note"] = item.Note,
                ["paymentMethod"] = item.PaymentMethod,
                ["paymentMask"] = item.PaymentMask,
                ["initiatorRole"] = item.InitiatorRole,
                ["waitingOnRole"] = item.WaitingOnRole,
                ["settlementStatus"] = item.SettlementStatus,
                ["vendorRecoveryStatus"] = item.VendorRecoveryStatus,
                ["vendorRecoveredAmount"] = item.VendorRecoveredAmount.ToString("0.##"),
                ["vendorOutstandingAmount"] = item.VendorOutstandingAmount.ToString("0.##")
            });

        return ExportFileResult.From(file);
    }

    [HttpGet("{caseId:guid}")]
    public async Task<ActionResult<AdminOrderSupportCaseListItemDto>> GetCase(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var result = await RequireCaseAsync(caseId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{caseId:guid}/assign")]
    public async Task<ActionResult<AdminOrderSupportCaseListItemDto>> Assign(
        Guid caseId,
        [FromBody] AdminOrderSupportCaseAssignRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        await _orderSupportCaseWorkflowService.AssignAsync(
            caseId,
            GetRequiredAdminUserId(),
            request.AssignedAdminId,
            request.Note,
            request.Priority,
            request.SlaDueAtUtc,
            cancellationToken);

        return Ok(await RequireCaseAsync(caseId, cancellationToken));
    }

    [HttpPost("{caseId:guid}/request-evidence")]
    public async Task<ActionResult<AdminOrderSupportCaseListItemDto>> RequestEvidence(
        Guid caseId,
        [FromBody] AdminOrderSupportCaseRequestEvidenceRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        await _orderSupportCaseWorkflowService.RequestEvidenceAsync(
            caseId,
            GetRequiredAdminUserId(),
            request.Note,
            request.CustomerVisibleNote,
            request.TargetRole,
            request.SlaDueAtUtc,
            cancellationToken);

        return Ok(await RequireCaseAsync(caseId, cancellationToken));
    }

    [HttpPost("{caseId:guid}/escalate")]
    public async Task<ActionResult<AdminOrderSupportCaseListItemDto>> Escalate(
        Guid caseId,
        [FromBody] AdminOrderSupportCaseEscalateRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        await _orderSupportCaseWorkflowService.EscalateAsync(
            caseId,
            GetRequiredAdminUserId(),
            request.Queue,
            request.Priority,
            request.Note,
            request.CustomerVisibleNote,
            request.NotifyEscalatedTeam,
            request.NotifyCurrentReviewer,
            request.SlaDueAtUtc,
            cancellationToken);

        return Ok(await RequireCaseAsync(caseId, cancellationToken));
    }

    [HttpPost("{caseId:guid}/approve")]
    public async Task<ActionResult<AdminOrderSupportCaseListItemDto>> Approve(
        Guid caseId,
        [FromBody] AdminOrderSupportCaseApproveRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        await _orderSupportCaseWorkflowService.ApproveAsync(
            caseId,
            GetRequiredAdminUserId(),
            request.RefundAmount,
            request.RefundMethod,
            request.CostBearer,
            request.DecisionNotes,
            request.CustomerVisibleNote,
            cancellationToken);

        return Ok(await RequireCaseAsync(caseId, cancellationToken));
    }

    [HttpPost("{caseId:guid}/reject")]
    public async Task<ActionResult<AdminOrderSupportCaseListItemDto>> Reject(
        Guid caseId,
        [FromBody] AdminOrderSupportCaseRejectRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        await _orderSupportCaseWorkflowService.RejectAsync(
            caseId,
            GetRequiredAdminUserId(),
            request.DecisionNotes,
            request.CustomerVisibleNote,
            cancellationToken);

        return Ok(await RequireCaseAsync(caseId, cancellationToken));
    }

    [HttpPost("{caseId:guid}/resolve")]
    public async Task<ActionResult<AdminOrderSupportCaseListItemDto>> Resolve(
        Guid caseId,
        [FromBody] AdminOrderSupportCaseResolveRequest? request,
        CancellationToken cancellationToken = default)
    {
        await _orderSupportCaseWorkflowService.ResolveAsync(
            caseId,
            GetRequiredAdminUserId(),
            request?.Note,
            cancellationToken);

        return Ok(await RequireCaseAsync(caseId, cancellationToken));
    }

    [HttpPost("{caseId:guid}/reopen")]
    public async Task<ActionResult<AdminOrderSupportCaseListItemDto>> Reopen(
        Guid caseId,
        [FromBody] AdminOrderSupportCaseReopenRequest? request,
        CancellationToken cancellationToken = default)
    {
        await _orderSupportCaseWorkflowService.ReopenAsync(
            caseId,
            GetRequiredAdminUserId(),
            request?.Note,
            cancellationToken);

        return Ok(await RequireCaseAsync(caseId, cancellationToken));
    }

    [HttpPost("{caseId:guid}/note")]
    public async Task<ActionResult<AdminOrderSupportCaseListItemDto>> AddNote(
        Guid caseId,
        [FromBody] AdminOrderSupportCaseNoteRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            throw new BadRequestException("INVALID_NOTE", "Note is required.");
        }

        await _orderSupportCaseWorkflowService.AddNoteAsync(
            caseId,
            GetRequiredAdminUserId(),
            request.Note,
            request.VisibleToCustomer,
            cancellationToken);

        return Ok(await RequireCaseAsync(caseId, cancellationToken));
    }

    [HttpPost("{caseId:guid}/messages")]
    public async Task<ActionResult<AdminOrderSupportCaseListItemDto>> AddMessage(
        Guid caseId,
        [FromBody] AdminOrderSupportCaseMessageRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Message is required.");
        }

        await _orderSupportCaseWorkflowService.AddAdminPublicMessageAsync(
            caseId,
            GetRequiredAdminUserId(),
            request.Message,
            request.Audience,
            cancellationToken);

        return Ok(await RequireCaseAsync(caseId, cancellationToken));
    }

    private Guid GetRequiredAdminUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
    }

    private async Task<AdminOrderSupportCaseListItemDto> RequireCaseAsync(Guid caseId, CancellationToken cancellationToken)
    {
        return await _orderReadService.GetAdminOrderSupportCaseDetailAsync(caseId, cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", caseId);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminOrderCaseStatsResponse>> GetStats(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var activeCasesQuery = _dbContext.OrderSupportCases
            .AsNoTracking()
            .Where(c => c.Status != OrderSupportCaseStatus.Rejected && c.Status != OrderSupportCaseStatus.Resolved);

        var totalOpen = await activeCasesQuery.CountAsync(cancellationToken);

        var byStatus = await MapEnumGroupCountsAsync(
            _dbContext.OrderSupportCases.AsNoTracking(),
            entity => entity.Status,
            cancellationToken);

        var byPriority = await MapEnumGroupCountsAsync(
            activeCasesQuery,
            entity => entity.Priority,
            cancellationToken);

        var byQueue = await MapEnumGroupCountsAsync(
            activeCasesQuery,
            entity => entity.Queue,
            cancellationToken);

        var byType = await MapEnumGroupCountsAsync(
            activeCasesQuery,
            entity => entity.Type,
            cancellationToken);

        var slaBreachedCount = await activeCasesQuery
            .Where(c => c.SlaDueAtUtc != null && c.SlaDueAtUtc < now)
            .CountAsync(cancellationToken);

        var closedCases = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Where(c => c.ClosedAtUtc != null)
            .Select(c => new { c.CreatedAtUtc, ClosedAtUtc = c.ClosedAtUtc!.Value })
            .ToListAsync(cancellationToken);

        var avgResolutionHours = closedCases.Count == 0
            ? 0
            : closedCases.Average(item => (item.ClosedAtUtc - item.CreatedAtUtc).TotalHours);

        return Ok(new AdminOrderCaseStatsResponse(
            totalOpen,
            slaBreachedCount,
            Math.Round(avgResolutionHours, 1),
            byStatus,
            byPriority,
            byQueue,
            byType));
    }

    private static async Task<List<AdminCaseCountByLabel>> MapEnumGroupCountsAsync<TEnum>(
        IQueryable<OrderSupportCase> query,
        System.Linq.Expressions.Expression<Func<OrderSupportCase, TEnum>> keySelector,
        CancellationToken cancellationToken)
        where TEnum : struct, Enum
    {
        var groups = await query
            .GroupBy(keySelector)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return groups
            .Select(group => new AdminCaseCountByLabel(group.Key.ToString(), group.Count))
            .ToList();
    }
}

public sealed record AdminOrderSupportCaseAssignRequest(
    Guid? AssignedAdminId,
    string? Note,
    string? Priority,
    DateTime? SlaDueAtUtc);

public sealed record AdminOrderSupportCaseRequestEvidenceRequest(
    string? Note,
    string? CustomerVisibleNote,
    string? TargetRole,
    DateTime? SlaDueAtUtc);

public sealed record AdminOrderSupportCaseEscalateRequest(
    string? Queue,
    string? Priority,
    string? Note,
    string? CustomerVisibleNote,
    bool NotifyEscalatedTeam,
    bool NotifyCurrentReviewer,
    DateTime? SlaDueAtUtc);

public sealed record AdminOrderSupportCaseApproveRequest(
    decimal? RefundAmount,
    string? RefundMethod,
    string? CostBearer,
    string? DecisionNotes,
    string? CustomerVisibleNote);

public sealed record AdminOrderSupportCaseRejectRequest(
    string? DecisionNotes,
    string? CustomerVisibleNote);

public sealed record AdminOrderSupportCaseResolveRequest(string? Note);

public sealed record AdminOrderSupportCaseReopenRequest(string? Note);

public sealed record AdminOrderSupportCaseNoteRequest(
    string Note,
    bool VisibleToCustomer);

public sealed record AdminOrderSupportCaseMessageRequest(
    string Message,
    string Audience = "customer,vendor,driver");

public sealed record AdminOrderCaseStatsResponse(
    int TotalOpen,
    int SlaBreachedCount,
    double AvgResolutionHours,
    List<AdminCaseCountByLabel> ByStatus,
    List<AdminCaseCountByLabel> ByPriority,
    List<AdminCaseCountByLabel> ByQueue,
    List<AdminCaseCountByLabel> ByType);

public sealed record AdminCaseCountByLabel(string Label, int Count);
