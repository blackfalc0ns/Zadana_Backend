using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
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
            .Select(c => new
            {
                c.Id,
                c.OrderId,
                OrderNumber = c.Order.OrderNumber,
                Type = c.Type,
                Status = c.Status,
                Priority = c.Priority,
                c.ReasonCode,
                c.Message,
                c.VendorResponse,
                c.VendorRespondedAtUtc,
                c.CustomerVisibleNote,
                c.InitiatorRole,
                c.AwaitingResponseFromRole,
                c.RequestedRefundAmount,
                c.ApprovedRefundAmount,
                c.CompensationType,
                c.CompensationCouponId,
                c.CreatedAtUtc,
                c.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var couponIds = cases
            .Where(item => item.CompensationCouponId.HasValue)
            .Select(item => item.CompensationCouponId!.Value)
            .Distinct()
            .ToList();

        var redeemedCouponIds = couponIds.Count == 0
            ? new HashSet<Guid>()
            : await _dbContext.Orders
                .AsNoTracking()
                .Where(order => order.CouponId.HasValue && couponIds.Contains(order.CouponId.Value))
                .Select(order => order.CouponId!.Value)
                .Distinct()
                .ToHashSetAsync(cancellationToken);

        var items = cases
            .Select(c => new VendorOrderCaseListItemResponse(
                c.Id,
                c.OrderId,
                c.OrderNumber,
                c.Type.ToString(),
                c.Status.ToString(),
                c.Priority.ToString(),
                c.ReasonCode,
                c.Message,
                c.VendorResponse,
                c.VendorRespondedAtUtc,
                c.CustomerVisibleNote,
                c.InitiatorRole,
                c.AwaitingResponseFromRole,
                c.RequestedRefundAmount,
                c.ApprovedRefundAmount,
                MapCompensationType(c.CompensationType),
                ResolveSettlementStatus(
                    c.Status,
                    c.Type,
                    c.CompensationType,
                    c.CompensationCouponId.HasValue && redeemedCouponIds.Contains(c.CompensationCouponId.Value)),
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .ToList();

        return Ok(new VendorOrderCasesListResponse(items, page, pageSize, total));
    }

    [HttpGet("{caseId:guid}")]
    public async Task<ActionResult<VendorOrderCaseDetailResponse>> GetCaseDetail(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

        var supportCase = await _dbContext.OrderSupportCases
            .AsNoTracking()
            .Include(c => c.Order)
            .Include(c => c.Attachments)
            .Include(c => c.Activities)
            .Where(c => c.Id == caseId && c.Order.VendorId == vendorId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("OrderSupportCase", caseId);

        var attachments = supportCase.Attachments
            .Select(a => new VendorOrderCaseAttachmentResponse(a.FileName, a.FileUrl))
            .ToList();

        var activities = supportCase.Activities
            .OrderByDescending(a => a.CreatedAtUtc)
            .Where(a => a.IsVisibleToRole("vendor"))
            .Select(a => new VendorOrderCaseActivityResponse(
                a.Id,
                a.Action,
                a.MessageType,
                a.Title,
                LocalizeVendorActivityNote(a.Action, a.MessageType, a.Note, supportCase, isArabic),
                a.ActorRole,
                a.CreatedAtUtc))
            .ToList();

        var coupon = supportCase.CompensationCouponId.HasValue
            ? await _dbContext.Coupons
                .AsNoTracking()
                .Where(item => item.Id == supportCase.CompensationCouponId.Value)
                .Select(item => new
                {
                    item.Code,
                    item.EndsAtUtc
                })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var couponRedeemed = supportCase.CompensationCouponId.HasValue &&
            await _dbContext.Orders
                .AsNoTracking()
                .AnyAsync(
                    order => order.CouponId == supportCase.CompensationCouponId.Value,
                    cancellationToken);

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
            LocalizeVendorCustomerVisibleNote(supportCase, couponRedeemed, isArabic),
            LocalizeVendorDecisionNotes(supportCase, couponRedeemed, isArabic),
            supportCase.InitiatorRole,
            supportCase.AwaitingResponseFromRole,
            supportCase.RequestedRefundAmount,
            supportCase.ApprovedRefundAmount,
            supportCase.RefundMethod,
            MapCompensationType(supportCase.CompensationType),
            ResolveSettlementStatus(supportCase.Status, supportCase.Type, supportCase.CompensationType, couponRedeemed),
            coupon?.Code,
            coupon?.EndsAtUtc,
            couponRedeemed,
            supportCase.CostBearer,
            supportCase.CreatedAtUtc,
            supportCase.UpdatedAtUtc,
            supportCase.ClosedAtUtc,
            BuildParticipants(supportCase),
            supportCase.Status is OrderSupportCaseStatus.Rejected or OrderSupportCaseStatus.Resolved ? [] : ["message"],
            attachments,
            activities,
            supportCase.Activities
                .OrderByDescending(activity => activity.CreatedAtUtc)
                .Where(activity => activity.IsVisibleToRole("vendor"))
                .Select(activity => new VendorOrderCaseMessageResponse(
                activity.Id,
                activity.Action,
                activity.MessageType,
                activity.Title,
                LocalizeVendorActivityNote(activity.Action, activity.MessageType, activity.Note, supportCase, isArabic),
                activity.ActorRole,
                activity.GetVisibleRoles().ToList(),
                activity.IsInternalOnly,
                activity.CreatedAtUtc,
                []))
                .ToList()));
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

    [HttpPost("{caseId:guid}/messages")]
    public Task<ActionResult<VendorOrderCaseRespondResponse>> SendMessage(
        Guid caseId,
        [FromBody] VendorOrderCaseRespondRequest? request,
        CancellationToken cancellationToken = default) =>
        Respond(caseId, request, cancellationToken);

    private static List<VendorOrderCaseParticipantResponse> BuildParticipants(Zadana.Domain.Modules.Orders.Entities.OrderSupportCase supportCase)
    {
        var participants = new List<VendorOrderCaseParticipantResponse>
        {
            new("customer", supportCase.InitiatorRole == "customer", supportCase.AwaitingResponseFromRole == "customer"),
            new("vendor", supportCase.InitiatorRole == "vendor", supportCase.AwaitingResponseFromRole == "vendor")
        };

        if (supportCase.Type is OrderSupportCaseType.DriverReport or OrderSupportCaseType.DriverDispute ||
            supportCase.Activities.Any(activity => activity.ActorRole == "driver") ||
            !string.IsNullOrWhiteSpace(supportCase.DriverResponse))
        {
            participants.Add(new("driver", supportCase.InitiatorRole == "driver", supportCase.AwaitingResponseFromRole == "driver"));
        }

        return participants;
    }

    private static string? ResolveSettlementStatus(
        OrderSupportCaseStatus caseStatus,
        OrderSupportCaseType caseType,
        OrderSupportCaseCompensationType? compensationType,
        bool couponRedeemed)
    {
        if (caseType != OrderSupportCaseType.ReturnRequest)
        {
            return null;
        }

        return caseStatus switch
        {
            OrderSupportCaseStatus.Submitted or
            OrderSupportCaseStatus.InReview or
            OrderSupportCaseStatus.AwaitingCustomerEvidence => "pending_review",
            OrderSupportCaseStatus.Rejected => "rejected",
            OrderSupportCaseStatus.Approved or
            OrderSupportCaseStatus.Resolved => compensationType switch
            {
                OrderSupportCaseCompensationType.CashRefund => "cash_refunded",
                OrderSupportCaseCompensationType.CouponCompensation when couponRedeemed => "coupon_redeemed",
                OrderSupportCaseCompensationType.CouponCompensation => "coupon_issued",
                _ => "approved"
            },
            _ => null
        };
    }

    private static string? MapCompensationType(OrderSupportCaseCompensationType? compensationType) =>
        compensationType switch
        {
            OrderSupportCaseCompensationType.CashRefund => "cash_refund",
            OrderSupportCaseCompensationType.CouponCompensation => "coupon_compensation",
            _ => null
        };

    private static string? LocalizeVendorCustomerVisibleNote(
        Zadana.Domain.Modules.Orders.Entities.OrderSupportCase supportCase,
        bool couponRedeemed,
        bool isArabic)
    {
        if (isArabic || !ContainsArabic(supportCase.CustomerVisibleNote))
        {
            return supportCase.CustomerVisibleNote;
        }

        return supportCase.Status switch
        {
            OrderSupportCaseStatus.Approved => "Your return request has been approved. You will be notified when the financial processing begins.",
            OrderSupportCaseStatus.Rejected => "Your return request was rejected after review.",
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CashRefund
                => "Your refund has been completed and the case is now closed.",
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CouponCompensation && couponRedeemed
                => "Your compensation coupon was redeemed and the case is now closed.",
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CouponCompensation
                => "A compensation coupon was issued and the case is now closed.",
            OrderSupportCaseStatus.AwaitingCustomerEvidence => "More information is required to continue reviewing this case.",
            _ => "Your support case has been updated."
        };
    }

    private static string? LocalizeVendorDecisionNotes(
        Zadana.Domain.Modules.Orders.Entities.OrderSupportCase supportCase,
        bool couponRedeemed,
        bool isArabic)
    {
        if (isArabic || !ContainsArabic(supportCase.DecisionNotes))
        {
            return supportCase.DecisionNotes;
        }

        return supportCase.Status switch
        {
            OrderSupportCaseStatus.Approved when supportCase.Type == OrderSupportCaseType.ReturnRequest
                => "The request matches platform policy and was approved after evidence review.",
            OrderSupportCaseStatus.Rejected => "The request did not meet the approval criteria after evidence review.",
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CashRefund
                => "The case was closed after completing the customer refund.",
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CouponCompensation && couponRedeemed
                => "The case was closed after the compensation coupon was redeemed.",
            OrderSupportCaseStatus.Resolved when supportCase.CompensationType == OrderSupportCaseCompensationType.CouponCompensation
                => "The case was closed after issuing the compensation coupon.",
            OrderSupportCaseStatus.AwaitingCustomerEvidence => "Additional evidence is required before a final decision can be made.",
            _ => "The support team reviewed this case and recorded an internal decision."
        };
    }

    private static string? LocalizeVendorActivityNote(
        string action,
        string messageType,
        string? note,
        Zadana.Domain.Modules.Orders.Entities.OrderSupportCase supportCase,
        bool isArabic)
    {
        if (isArabic || !ContainsArabic(note))
        {
            return note;
        }

        return (action.Trim().ToLowerInvariant(), messageType.Trim().ToLowerInvariant()) switch
        {
            ("approved", _) => LocalizeVendorCustomerVisibleNote(supportCase, couponRedeemed: false, isArabic: false),
            ("rejected", _) => "The case was rejected after review.",
            ("resolved", _) => "The case was resolved and closed.",
            ("request_evidence", _) => "Additional information was requested to continue the review.",
            ("escalated", _) => "The case was escalated for further review.",
            ("admin_message", _) => "Support shared an update on this case.",
            (_, "decision") => "A final decision was recorded for this case.",
            (_, "public_note") => "A public note was added to this case.",
            _ => note
        };
    }

    private static bool ContainsArabic(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, @"[\u0600-\u06FF]");
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
    string? WaitingOnRole,
    decimal? RequestedRefundAmount,
    decimal? ApprovedRefundAmount,
    string? CompensationType,
    string? SettlementStatus,
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
    string? WaitingOnRole,
    decimal? RequestedRefundAmount,
    decimal? ApprovedRefundAmount,
    string? RefundMethod,
    string? CompensationType,
    string? SettlementStatus,
    string? CouponCode,
    DateTime? CouponExpiresAtUtc,
    bool CouponRedeemed,
    string? CostBearer,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ClosedAt,
    List<VendorOrderCaseParticipantResponse> Participants,
    List<string> AllowedActions,
    List<VendorOrderCaseAttachmentResponse> Attachments,
    List<VendorOrderCaseActivityResponse> Activities,
    List<VendorOrderCaseMessageResponse> Messages);

public sealed record VendorOrderCaseAttachmentResponse(string FileName, string FileUrl);
public sealed record VendorOrderCaseActivityResponse(Guid Id, string Action, string MessageType, string Title, string? Note, string ActorRole, DateTime CreatedAt);
public sealed record VendorOrderCaseMessageResponse(Guid Id, string Action, string MessageType, string Title, string? Body, string AuthorRole, List<string> VisibleTo, bool IsInternalOnly, DateTime CreatedAt, List<VendorOrderCaseAttachmentResponse> Attachments);
public sealed record VendorOrderCaseParticipantResponse(string Role, bool IsInitiator, bool IsAwaitingResponse);
public sealed record VendorOrderCaseRespondResponse(Guid CaseId, string Response, DateTime RespondedAt, string CaseStatus);
