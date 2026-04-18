using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Social.Support;

namespace Zadana.Application.Modules.Social.Queries;

// DTOs
public record NotificationDto(
    Guid Id,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string? Type,
    Guid? ReferenceId,
    string? Data,
    JsonElement? DataObject,
    bool IsRead,
    DateTime CreatedAtUtc);

public record NotificationListDto(
    List<NotificationDto> Items,
    int Page,
    int PerPage,
    int Total,
    int UnreadCount,
    bool HasMore);

// Get Notifications (paginated)
public record GetNotificationsQuery(
    Guid UserId,
    int Page = 1,
    int PerPage = 20,
    string? Type = null,
    bool? IsRead = null,
    DateTime? CreatedFromUtc = null,
    DateTime? CreatedToUtc = null) : IRequest<NotificationListDto>;

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, NotificationListDto>
{
    private readonly IApplicationDbContext _context;

    public GetNotificationsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<NotificationListDto> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var perPage = Math.Clamp(request.PerPage, 1, 100);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = request.Type.Trim();
            query = query.Where(x => x.Type == type);
        }

        if (request.IsRead.HasValue)
        {
            query = query.Where(x => x.IsRead == request.IsRead.Value);
        }

        if (request.CreatedFromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= request.CreatedFromUtc.Value);
        }

        if (request.CreatedToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc <= request.CreatedToUtc.Value);
        }

        query = query
            .OrderByDescending(x => x.CreatedAtUtc);

        var unreadCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(x => x.UserId == request.UserId && !x.IsRead, cancellationToken);

        var total = await query.CountAsync(cancellationToken);
        var rawItems = await query
            .Skip((page - 1) * perPage)
            .Take(perPage + 1)
            .Select(x => new NotificationDto(
                x.Id,
                x.TitleAr,
                x.TitleEn,
                x.BodyAr,
                x.BodyEn,
                x.Type,
                x.ReferenceId,
                x.Data,
                null,
                x.IsRead,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var hasMore = rawItems.Count > perPage;
        var items = rawItems
            .Take(perPage)
            .Select(item => item with { DataObject = NotificationPayloadHelper.TryParseData(item.Data) })
            .ToList();

        return new NotificationListDto(items, page, perPage, total, unreadCount, hasMore);
    }
}

// Get Unread Count
public record GetUnreadNotificationCountQuery(Guid UserId) : IRequest<int>;

public class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, int>
{
    private readonly IApplicationDbContext _context;

    public GetUnreadNotificationCountQueryHandler(IApplicationDbContext context) => _context = context;

    public Task<int> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken) =>
        _context.Notifications.CountAsync(x => x.UserId == request.UserId && !x.IsRead, cancellationToken);
}

// Mark as Read
public record MarkNotificationReadCommand(Guid NotificationId, Guid UserId) : IRequest;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand>
{
    private readonly IApplicationDbContext _context;

    public MarkNotificationReadCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        await _context.Notifications
            .Where(x => x.Id == request.NotificationId && x.UserId == request.UserId && !x.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsRead, _ => true),
                cancellationToken);
    }
}

// Mark All as Read
public record MarkAllNotificationsReadCommand(Guid UserId) : IRequest<int>;

public class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly IApplicationDbContext _context;

    public MarkAllNotificationsReadCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<int> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        return await _context.Notifications
            .Where(x => x.UserId == request.UserId && !x.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsRead, _ => true),
                cancellationToken);
    }
}
