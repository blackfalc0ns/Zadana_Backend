using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Vendors.Controllers;

[Route("api/vendor/support/tickets")]
[Tags("Vendor App API")]
[Authorize(Policy = "VendorOnly")]
public class VendorSupportTicketsController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentVendorService _currentVendorService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAdminAlertService _adminAlertService;

    public VendorSupportTicketsController(
        IApplicationDbContext dbContext,
        ICurrentVendorService currentVendorService,
        ICurrentUserService currentUserService,
        IAdminAlertService adminAlertService)
    {
        _dbContext = dbContext;
        _currentVendorService = currentVendorService;
        _currentUserService = currentUserService;
        _adminAlertService = adminAlertService;
    }

    [HttpGet]
    public async Task<ActionResult<VendorSupportTicketsListResponse>> GetTickets(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _dbContext.VendorSupportTickets
            .AsNoTracking()
            .Include(ticket => ticket.Order)
            .Where(ticket => ticket.VendorId == vendorId);

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
                ? query.Where(ticket => ticket.Id == parsedId || ticket.OrderId == parsedId)
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
        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        return Ok(await RequireVendorTicketAsync(vendorId, ticketId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<VendorSupportTicketResponse>> CreateTicket(
        [FromBody] CreateVendorSupportTicketRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var subject = RequireText(request.Subject, "Subject");
        var message = RequireText(request.Message, "Message");
        var category = VendorSupportTicketContractMapper.NormalizeCategory(request.Category);
        var priority = VendorSupportTicketContractMapper.ParsePriority(request.Priority);

        if (request.OrderId.HasValue)
        {
            var orderBelongsToVendor = await _dbContext.Orders
                .AsNoTracking()
                .AnyAsync(order => order.Id == request.OrderId.Value && order.VendorId == vendorId, cancellationToken);

            if (!orderBelongsToVendor)
            {
                throw new NotFoundException("Order", request.OrderId.Value);
            }
        }

        var ticket = new VendorSupportTicket(
            vendorId,
            userId,
            BuildReference(),
            subject,
            category,
            priority,
            message,
            request.OrderId);

        _dbContext.VendorSupportTickets.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await NotifyAdminsAsync(ticket, "created", cancellationToken);

        var response = await RequireVendorTicketAsync(vendorId, ticket.Id, cancellationToken);
        return CreatedAtAction(nameof(GetTicket), new { ticketId = ticket.Id }, response);
    }

    [HttpPost("{ticketId:guid}/messages")]
    public async Task<ActionResult<VendorSupportTicketResponse>> AddMessage(
        Guid ticketId,
        [FromBody] VendorSupportTicketMessageRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var vendorId = await _currentVendorService.GetRequiredVendorIdAsync(cancellationToken);
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var message = RequireText(request.Message, "Message");

        var ticket = await _dbContext.VendorSupportTickets
            .Include(item => item.Messages)
            .FirstOrDefaultAsync(item => item.Id == ticketId && item.VendorId == vendorId, cancellationToken)
            ?? throw new NotFoundException("VendorSupportTicket", ticketId);

        ticket.AddVendorMessage(userId, message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await NotifyAdminsAsync(ticket, "vendor_replied", cancellationToken);

        return Ok(await RequireVendorTicketAsync(vendorId, ticket.Id, cancellationToken));
    }

    private async Task<VendorSupportTicketResponse> RequireVendorTicketAsync(
        Guid vendorId,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var ticket = await _dbContext.VendorSupportTickets
            .AsNoTracking()
            .Include(item => item.Order)
            .Include(item => item.Messages)
            .FirstOrDefaultAsync(item => item.Id == ticketId && item.VendorId == vendorId, cancellationToken)
            ?? throw new NotFoundException("VendorSupportTicket", ticketId);

        return VendorSupportTicketContractMapper.Map(ticket);
    }

    private async Task NotifyAdminsAsync(
        VendorSupportTicket ticket,
        string action,
        CancellationToken cancellationToken)
    {
        var vendorName = await _dbContext.Vendors
            .AsNoTracking()
            .Where(vendor => vendor.Id == ticket.VendorId)
            .Select(vendor => vendor.BusinessNameAr ?? vendor.BusinessNameEn)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "Vendor";

        var targetUrl = "/notifications?category=support";
        var isCreated = string.Equals(action, "created", StringComparison.OrdinalIgnoreCase);

        await _adminAlertService.SendAsync(
            new AdminAlertRequest(
                isCreated ? AdminAlertTypes.VendorSupportTicketCreated : AdminAlertTypes.VendorSupportTicketUpdated,
                AdminAlertCategories.Support,
                ResolveAdminPriority(ticket.Priority),
                isCreated ? "تذكرة دعم تاجر جديدة" : "رد جديد على دعم التاجر",
                isCreated ? "New vendor support ticket" : "Vendor support ticket updated",
                isCreated
                    ? $"فتح {vendorName} تذكرة دعم جديدة برقم {ticket.Reference}."
                    : $"أضاف {vendorName} ردا على تذكرة الدعم {ticket.Reference}.",
                isCreated
                    ? $"{vendorName} opened vendor support ticket {ticket.Reference}."
                    : $"{vendorName} replied to vendor support ticket {ticket.Reference}.",
                ticket.Id,
                targetUrl,
                new
                {
                    source = "vendor_support",
                    ticketId = ticket.Id,
                    ticket.Reference,
                    ticket.VendorId,
                    ticket.OrderId,
                    action,
                    targetUrl
                }),
            cancellationToken);
    }

    private static string ResolveAdminPriority(Domain.Modules.Vendors.Enums.VendorSupportTicketPriority priority) =>
        priority switch
        {
            Domain.Modules.Vendors.Enums.VendorSupportTicketPriority.Urgent => AdminAlertPriorities.Critical,
            Domain.Modules.Vendors.Enums.VendorSupportTicketPriority.High => AdminAlertPriorities.High,
            Domain.Modules.Vendors.Enums.VendorSupportTicketPriority.Low => AdminAlertPriorities.Low,
            _ => AdminAlertPriorities.Normal
        };

    private static string RequireText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException($"INVALID_{fieldName.ToUpperInvariant()}", $"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string BuildReference() =>
        $"SUP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
