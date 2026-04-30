using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
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

    public AdminOrderCasesController(
        ICurrentUserService currentUserService,
        IOrderReadService orderReadService,
        IOrderSupportCaseWorkflowService orderSupportCaseWorkflowService)
    {
        _currentUserService = currentUserService;
        _orderReadService = orderReadService;
        _orderSupportCaseWorkflowService = orderSupportCaseWorkflowService;
    }

    [HttpGet]
    public async Task<ActionResult<AdminOrderSupportCasesListDto>> GetCases(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? queue,
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
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
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

    private Guid GetRequiredAdminUserId()
    {
        return _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
    }

    private async Task<AdminOrderSupportCaseListItemDto> RequireCaseAsync(Guid caseId, CancellationToken cancellationToken)
    {
        return await _orderReadService.GetAdminOrderSupportCaseDetailAsync(caseId, cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", caseId);
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

public sealed record AdminOrderSupportCaseNoteRequest(
    string Note,
    bool VisibleToCustomer);
