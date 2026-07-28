using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/admin/vendor-support-tickets")]
[Tags("Admin Dashboard API")]
[Authorize(Policy = "AdminOnly")]
public class AdminVendorSupportTicketsController : ApiControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly ILogger<AdminVendorSupportTicketsController> _logger;

    public AdminVendorSupportTicketsController(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        ILogger<AdminVendorSupportTicketsController> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _logger = logger;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminVendorSupportTicketStatsResponse>> GetStats(
        CancellationToken cancellationToken = default)
    {
        var tickets = _dbContext.VendorSupportTickets.AsNoTracking();

        var totalOpen = await tickets
            .CountAsync(ticket => ticket.Status != Domain.Modules.Vendors.Enums.VendorSupportTicketStatus.Resolved, cancellationToken);

        var waitingVendor = await tickets
            .CountAsync(ticket => ticket.Status == Domain.Modules.Vendors.Enums.VendorSupportTicketStatus.WaitingVendor, cancellationToken);

        var resolved = await tickets
            .CountAsync(ticket => ticket.Status == Domain.Modules.Vendors.Enums.VendorSupportTicketStatus.Resolved, cancellationToken);

        return Ok(new AdminVendorSupportTicketStatsResponse(totalOpen, waitingVendor, resolved));
    }

    [HttpGet]
    public async Task<ActionResult<VendorSupportTicketsListResponse>> GetTickets(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] Guid? vendorId,
        [FromQuery] Guid? orderId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.VendorSupportTickets
            .AsNoTracking()
            .Include(ticket => ticket.Order)
            .Where(ticket => true);

        if (vendorId.HasValue)
        {
            query = query.Where(ticket => ticket.VendorId == vendorId.Value);
        }

        if (orderId.HasValue)
        {
            query = query.Where(ticket => ticket.OrderId == orderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var parsedStatus = VendorSupportTicketContractMapper.ParseStatus(status);
            query = query.Where(ticket => ticket.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(priority) && !string.Equals(priority, "all", StringComparison.OrdinalIgnoreCase))
        {
            var parsedPriority = VendorSupportTicketContractMapper.ParsePriority(priority);
            query = query.Where(ticket => ticket.Priority == parsedPriority);
        }

        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedCategory = VendorSupportTicketContractMapper.NormalizeCategory(category);
            query = query.Where(ticket => ticket.Category == normalizedCategory);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            var pattern = $"%{trimmedSearch}%";

            query = Guid.TryParse(trimmedSearch, out var parsedId)
                ? query.Where(ticket => ticket.Id == parsedId || ticket.OrderId == parsedId || ticket.VendorId == parsedId)
                : query.Where(ticket =>
                    EF.Functions.Like(ticket.Reference, pattern) ||
                    EF.Functions.Like(ticket.Subject, pattern) ||
                    EF.Functions.Like(ticket.LastMessagePreview, pattern) ||
                    (ticket.Order != null && EF.Functions.Like(ticket.Order.OrderNumber, pattern)));
        }

        var total = await query.CountAsync(cancellationToken);
        var tickets = await query
            .OrderByDescending(ticket => ticket.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new VendorSupportTicketsListResponse(
            tickets.Select(ticket => VendorSupportTicketContractMapper.Map(ticket, includeMessages: false)).ToList(),
            page,
            pageSize,
            total));
    }

    [HttpGet("{ticketId:guid}")]
    public async Task<ActionResult<VendorSupportTicketResponse>> GetTicket(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        return Ok(await RequireTicketResponseAsync(ticketId, cancellationToken));
    }

    [HttpPost("{ticketId:guid}/assign")]
    public async Task<ActionResult<VendorSupportTicketResponse>> Assign(
        Guid ticketId,
        [FromBody] AdminVendorSupportTicketAssignRequest? request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = GetRequiredAdminUserId();
        var ticket = await RequireTrackedTicketAsync(ticketId, includeMessages: true, cancellationToken);
        ticket.Assign(adminUserId, request?.AssignedAdminId);
        await SaveTicketChangesAsync("assign", ticketId, cancellationToken);

        return Ok(await RequireTicketResponseAsync(ticketId, cancellationToken));
    }

    [HttpPost("{ticketId:guid}/messages")]
    public async Task<ActionResult<VendorSupportTicketResponse>> AddMessage(
        Guid ticketId,
        [FromBody] VendorSupportTicketMessageRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Message is required.");
        }

        var adminUserId = GetRequiredAdminUserId();
        var ticket = await RequireTrackedTicketAsync(ticketId, includeMessages: true, cancellationToken);
        ticket.AddAdminMessage(adminUserId, request.Message);
        await SaveTicketChangesAsync("message", ticketId, cancellationToken);

        var response = await RequireTicketResponseAsync(ticketId, cancellationToken);
        await TryNotifyVendorAsync(ticket.Id, "admin_message", request.Message, cancellationToken);

        return Ok(response);
    }

    [HttpPost("{ticketId:guid}/status")]
    public async Task<ActionResult<VendorSupportTicketResponse>> UpdateStatus(
        Guid ticketId,
        [FromBody] AdminVendorSupportTicketStatusRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var adminUserId = GetRequiredAdminUserId();
        var ticket = await RequireTrackedTicketAsync(ticketId, includeMessages: true, cancellationToken);
        var status = VendorSupportTicketContractMapper.ParseStatus(request.Status);

        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            ticket.AddAdminMessage(adminUserId, request.Message);
        }

        ticket.SetStatus(status);
        await SaveTicketChangesAsync("status", ticketId, cancellationToken);

        var response = await RequireTicketResponseAsync(ticketId, cancellationToken);
        await TryNotifyVendorAsync(ticket.Id, "status_changed", request.Message, cancellationToken);

        return Ok(response);
    }

    private Guid GetRequiredAdminUserId() =>
        _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

    /// <summary>
    /// Persists pending changes and, on failure, surfaces the real database
    /// error instead of an opaque 500. InMemory test databases don't enforce
    /// constraints, so provider-specific failures only appear in production;
    /// exposing the root message here makes them diagnosable for admins.
    /// </summary>
    private async Task SaveTicketChangesAsync(string operation, Guid ticketId, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var root = ex.GetBaseException();
            _logger.LogError(
                ex,
                "Vendor support ticket {Operation} failed to persist for ticket {TicketId}. Root: {RootMessage}",
                operation,
                ticketId,
                root.Message);

            throw new ExternalServiceException(
                "VENDOR_SUPPORT_TICKET_PERSIST_FAILED",
                $"Failed to save {operation} for ticket {ticketId}: {root.Message}",
                ex);
        }
    }

    private async Task<VendorSupportTicketResponse> RequireTicketResponseAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var ticket = await _dbContext.VendorSupportTickets
            .AsNoTracking()
            .Include(item => item.Order)
            .Include(item => item.Messages)
            .FirstOrDefaultAsync(item => item.Id == ticketId, cancellationToken)
            ?? throw new NotFoundException("VendorSupportTicket", ticketId);

        return VendorSupportTicketContractMapper.Map(ticket);
    }

    private async Task<VendorSupportTicket> RequireTrackedTicketAsync(
        Guid ticketId,
        bool includeMessages,
        CancellationToken cancellationToken,
        bool includeOrder = false)
    {
        var query = _dbContext.VendorSupportTickets.AsQueryable();
        if (includeMessages)
        {
            query = query.Include(item => item.Messages);
        }

        if (includeOrder)
        {
            query = query.Include(item => item.Order);
        }

        return await query.FirstOrDefaultAsync(item => item.Id == ticketId, cancellationToken)
            ?? throw new NotFoundException("VendorSupportTicket", ticketId);
    }

    private async Task TryNotifyVendorAsync(
        Guid ticketId,
        string action,
        string? message,
        CancellationToken cancellationToken)
    {
        try
        {
            await NotifyVendorAsync(ticketId, action, message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Vendor support ticket {TicketId} was updated, but vendor notification dispatch failed for action {Action}.",
                ticketId,
                action);
        }
    }

    private async Task NotifyVendorAsync(
        Guid ticketId,
        string action,
        string? message,
        CancellationToken cancellationToken)
    {
        var ticket = await _dbContext.VendorSupportTickets
            .AsNoTracking()
            .Include(item => item.Vendor)
            .Include(item => item.Order)
            .FirstOrDefaultAsync(item => item.Id == ticketId, cancellationToken);

        if (ticket is null)
        {
            _logger.LogWarning(
                "Skipped vendor support notification for ticket {TicketId} because it could not be reloaded.",
                ticketId);
            return;
        }

        if (ticket.Vendor?.UserId is not Guid vendorUserId || vendorUserId == Guid.Empty)
        {
            return;
        }

        var targetUrl = $"/support/tickets/{ticket.Id}";
        var (titleAr, titleEn, bodyAr, bodyEn) = ResolveVendorNotificationContent(ticket, action, message);
        var data = JsonSerializer.Serialize(new
        {
            source = "vendor_support",
            ticketId = ticket.Id,
            ticket.Reference,
            ticket.VendorId,
            ticket.OrderId,
            orderNumber = ticket.Order?.OrderNumber,
            action,
            status = VendorSupportTicketContractMapper.ToApiStatus(ticket.Status),
            targetUrl
        }, JsonOptions);

        var request = new NotificationDispatchRequest(
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            NotificationTypes.VendorSupportTicketChanged,
            NotificationCategories.Support,
            ResolveNotificationPriority(ticket.Priority),
            ticket.Id,
            data);

        await _notificationService.SendToUserAsync(vendorUserId, request, cancellationToken);

        await _oneSignalPushService.SendToExternalUserAsync(
            vendorUserId.ToString(),
            titleAr,
            titleEn,
            bodyAr,
            bodyEn,
            NotificationTypes.VendorSupportTicketChanged,
            ticket.Id,
            data,
            targetUrl,
            OneSignalPushProfile.Default,
            OneSignalApplicationTarget.VendorWeb,
            cancellationToken);
    }

    private static (string TitleAr, string TitleEn, string BodyAr, string BodyEn) ResolveVendorNotificationContent(
        VendorSupportTicket ticket,
        string action,
        string? message)
    {
        if (string.Equals(action, "admin_message", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "رد جديد من دعم التجار",
                "New vendor support reply",
                $"وصل رد جديد على تذكرة الدعم {ticket.Reference}.",
                $"A new reply was added to support ticket {ticket.Reference}.");
        }

        return ticket.Status switch
        {
            Domain.Modules.Vendors.Enums.VendorSupportTicketStatus.Resolved => (
                "حلّينا تذكرة الدعم",
                "Support ticket resolved",
                $"أغلقنا تذكرة الدعم {ticket.Reference} بعد المراجعة.",
                $"Support ticket {ticket.Reference} has been resolved."),
            Domain.Modules.Vendors.Enums.VendorSupportTicketStatus.WaitingVendor => (
                "الدعم بانتظار ردك",
                "Support is waiting for your reply",
                $"تذكرة الدعم {ticket.Reference} تحتاج إلى رد منك.",
                $"Support ticket {ticket.Reference} is waiting for your reply."),
            _ => (
                "تحديث على دعم التجار",
                "Vendor support ticket updated",
                $"حدّثنا تذكرة الدعم {ticket.Reference}.",
                $"Support ticket {ticket.Reference} has been updated.")
        };
    }

    private static string ResolveNotificationPriority(Domain.Modules.Vendors.Enums.VendorSupportTicketPriority priority) =>
        priority switch
        {
            Domain.Modules.Vendors.Enums.VendorSupportTicketPriority.Urgent => "critical",
            Domain.Modules.Vendors.Enums.VendorSupportTicketPriority.High => "high",
            Domain.Modules.Vendors.Enums.VendorSupportTicketPriority.Low => "low",
            _ => "normal"
        };
}

public sealed record AdminVendorSupportTicketStatsResponse(
    int TotalOpen,
    int WaitingVendor,
    int Resolved);
