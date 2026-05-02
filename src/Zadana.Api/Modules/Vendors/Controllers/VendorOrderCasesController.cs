using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/vendor/order-cases")]
[Tags("Vendor App API")]
[Authorize(Policy = "VendorOnly")]
public class VendorOrderCasesController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentVendorService _currentVendorService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOrderSupportCaseWorkflowService _workflowService;

    public VendorOrderCasesController(
        IApplicationDbContext dbContext,
        ICurrentVendorService currentVendorService,
        ICurrentUserService currentUserService,
        IOrderSupportCaseWorkflowService workflowService)
    {
        _dbContext = dbContext;
        _currentVendorService = currentVendorService;
        _currentUserService = currentUserService;
        _workflowService = workflowService;
    }

    [HttpGet]
    public async Task<ActionResult<VendorOrderCasesListResponse>> GetCases(
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _dbContext.OrderSupportCases
            .AsNoTracking()
            .Include(c => c.Order)
            .Where(c => c.Order.VendorId == vendorId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<OrderSupportCaseStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(c => c.Status == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (Enum.TryParse<OrderSupportCaseType>(type, ignoreCase: true, out var parsedType))
            {
                query = query.Where(c => c.Type == parsedType);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();

            if (Guid.TryParse(trimmedSearch, out var caseId))
            {
                query = query.Where(c => c.Id == caseId);
            }
            else
            {
                var pattern = $"%{trimmedSearch}%";
                query = query.Where(c =>
                    EF.Functions.Like(c.Order.OrderNumber, pattern) ||
                    (c.ReasonCode != null && EF.Functions.Like(c.ReasonCode, pattern)) ||
                    EF.Functions.Like(c.Message, pattern));
            }
        }

        var total = await query.CountAsync(cancellationToken);
        var cases = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new VendorOrderCaseListItemResponse(
                c.Id,
                c.OrderId,
                c.Order.OrderNumber,
                c.Type.ToString(),
                c.Status.ToString(),
                c.Priority.ToString(),
                c.ReasonCode,
                c.Message,
                c.VendorResponse,
                c.VendorRespondedAtUtc,
                c.CustomerVisibleNote,
                c.InitiatorRole,
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new VendorOrderCasesListResponse(cases, page, pageSize, total));
    }

    [HttpGet("{caseId:guid}")]
    public async Task<ActionResult<VendorOrderCaseDetailResponse>> GetCaseDetail(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);

        var supportCase = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Include(c => c.Order)
            .Include(c => c.Attachments)
            .Include(c => c.Activities.Where(a => a.VisibleToCustomer))
            .Where(c => c.Id == caseId && c.Order.VendorId == vendorId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", caseId);

        var attachments = supportCase.Attachments
            .Select(a => new VendorOrderCaseAttachmentResponse(a.FileName, a.FileUrl))
            .ToList();

        var activities = supportCase.Activities
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new VendorOrderCaseActivityResponse(
                a.Action,
                a.Title,
                a.Note,
                a.ActorRole,
                a.CreatedAtUtc))
            .ToList();

        return Ok(new VendorOrderCaseDetailResponse(
            supportCase.Id,
            supportCase.OrderId,
            supportCase.Order.OrderNumber,
            supportCase.Type.ToString(),
            supportCase.Status.ToString(),
            supportCase.Priority.ToString(),
            supportCase.Queue.ToString(),
            supportCase.ReasonCode,
            supportCase.Message,
            supportCase.VendorResponse,
            supportCase.VendorRespondedAtUtc,
            supportCase.CustomerVisibleNote,
            supportCase.DecisionNotes,
            supportCase.InitiatorRole,
            supportCase.RequestedRefundAmount,
            supportCase.ApprovedRefundAmount,
            supportCase.RefundMethod,
            supportCase.CostBearer,
            supportCase.CreatedAtUtc,
            supportCase.UpdatedAtUtc,
            supportCase.ClosedAtUtc,
            attachments,
            activities));
    }

    [HttpPost("{caseId:guid}/respond")]
    public async Task<ActionResult<VendorOrderCaseRespondResponse>> Respond(
        Guid caseId,
        [FromBody] VendorOrderCaseRespondRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Response))
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Response message is required.");
        }

        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var vendorUserId = _currentUserService.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        // Verify the case belongs to this vendor
        var caseExists = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .AnyAsync(c => c.Id == caseId && c.Order.VendorId == vendorId, cancellationToken);

        if (!caseExists)
        {
            throw new NotFoundException("OrderSupportCase", caseId);
        }

        var supportCase = await _workflowService.AddVendorResponseAsync(
            caseId,
            vendorUserId,
            request.Response,
            cancellationToken);

        return Ok(new VendorOrderCaseRespondResponse(
            supportCase.Id,
            supportCase.VendorResponse!,
            supportCase.VendorRespondedAtUtc!.Value,
            supportCase.Status.ToString()));
    }
}

// Request DTOs
public sealed record VendorOrderCaseRespondRequest(string Response);

// Response DTOs
public sealed record VendorOrderCasesListResponse(
    List<VendorOrderCaseListItemResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record VendorOrderCaseListItemResponse(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Type,
    string Status,
    string Priority,
    string? ReasonCode,
    string Message,
    string? VendorResponse,
    DateTime? VendorRespondedAt,
    string? CustomerVisibleNote,
    string InitiatorRole,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record VendorOrderCaseDetailResponse(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Type,
    string Status,
    string Priority,
    string Queue,
    string? ReasonCode,
    string Message,
    string? VendorResponse,
    DateTime? VendorRespondedAt,
    string? CustomerVisibleNote,
    string? DecisionNotes,
    string InitiatorRole,
    decimal? RequestedRefundAmount,
    decimal? ApprovedRefundAmount,
    string? RefundMethod,
    string? CostBearer,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ClosedAt,
    List<VendorOrderCaseAttachmentResponse> Attachments,
    List<VendorOrderCaseActivityResponse> Activities);

public sealed record VendorOrderCaseAttachmentResponse(string FileName, string FileUrl);
public sealed record VendorOrderCaseActivityResponse(string Action, string Title, string? Note, string ActorRole, DateTime CreatedAt);
public sealed record VendorOrderCaseRespondResponse(Guid CaseId, string Response, DateTime RespondedAt, string CaseStatus);
