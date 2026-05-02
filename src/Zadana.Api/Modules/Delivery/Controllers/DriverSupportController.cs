using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Interfaces;
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
            supportCase.Type.ToString(),
            supportCase.Status.ToString(),
            supportCase.Priority.ToString(),
            supportCase.ReasonCode,
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
            supportCase.Type.ToString(),
            supportCase.Status.ToString(),
            supportCase.Priority.ToString(),
            supportCase.ReasonCode,
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
            .Select(c => new DriverSupportCaseListItemResponse(
                c.Id,
                c.OrderId,
                c.Order.OrderNumber,
                c.Type.ToString(),
                c.Status.ToString(),
                c.Priority.ToString(),
                c.ReasonCode,
                c.Message,
                c.CustomerVisibleNote,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                c.ClosedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new DriverSupportCasesListResponse(cases, page, pageSize, total));
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
                a.Action,
                a.Title,
                a.Note,
                a.ActorRole,
                a.CreatedAtUtc))
            .ToList();

        var attachments = supportCase.Attachments
            .Select(a => new DriverSupportCaseAttachmentResponse(a.FileName, a.FileUrl))
            .ToList();

        return Ok(new DriverSupportCaseDetailResponse(
            supportCase.Id,
            supportCase.OrderId,
            supportCase.Order.OrderNumber,
            supportCase.Type.ToString(),
            supportCase.Status.ToString(),
            supportCase.Priority.ToString(),
            supportCase.Queue.ToString(),
            supportCase.ReasonCode,
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
            supportCase.Type.ToString(),
            supportCase.Status.ToString(),
            supportCase.Priority.ToString(),
            supportCase.ReasonCode,
            supportCase.Message,
            supportCase.CreatedAtUtc));
    }

    private async Task<(Guid DriverId, Guid UserId)> ResolveDriverAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");

        var driver = await _driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return (driver.Id, userId);
    }
}

// Request DTOs
public sealed record DriverReportIssueRequest(
    string? ReasonCode,
    string Message,
    List<DriverSupportAttachmentInput>? Attachments);

public sealed record DriverDisputeRequest(
    string? ReasonCode,
    string Message);

public sealed record DriverSupportAttachmentInput(string FileName, string FileUrl);

// Response DTOs
public sealed record DriverSupportCaseResponse(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Type,
    string Status,
    string Priority,
    string? ReasonCode,
    string Message,
    DateTime CreatedAt);

public sealed record DriverSupportCasesListResponse(
    List<DriverSupportCaseListItemResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record DriverSupportCaseListItemResponse(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Type,
    string Status,
    string Priority,
    string? ReasonCode,
    string Message,
    string? AdminNote,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ClosedAt);

public sealed record DriverSupportCaseDetailResponse(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Type,
    string Status,
    string Priority,
    string Queue,
    string? ReasonCode,
    string Message,
    string? AdminNote,
    string? DecisionNotes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ClosedAt,
    List<DriverSupportCaseAttachmentResponse> Attachments,
    List<DriverSupportCaseActivityResponse> Activities);

public sealed record DriverSupportCaseAttachmentResponse(string FileName, string FileUrl);
public sealed record DriverSupportCaseActivityResponse(string Action, string Title, string? Note, string ActorRole, DateTime CreatedAt);
