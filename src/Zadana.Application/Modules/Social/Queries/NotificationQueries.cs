using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;

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
    bool IsRead,
    DateTime CreatedAtUtc);

public record NotificationListDto(
    List<NotificationDto> Items,
    int Page,
    int PerPage,
    int Total);

// Get Notifications (paginated)
public record GetNotificationsQuery(
    Guid UserId,
    int Page = 1,
    int PerPage = 20) : IRequest<NotificationListDto>;

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, NotificationListDto>
{
    private readonly IApplicationDbContext _context;

    public GetNotificationsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<NotificationListDto> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId)
            .OrderByDescending(x => x.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PerPage)
            .Take(request.PerPage)
            .Select(x => new NotificationDto(
                x.Id,
                x.TitleAr,
                x.TitleEn,
                x.BodyAr,
                x.BodyEn,
                x.Type,
                x.ReferenceId,
                x.Data,
                x.IsRead,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new NotificationListDto(items, request.Page, request.PerPage, total);
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
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == request.NotificationId && x.UserId == request.UserId, cancellationToken);

        if (notification is not null && !notification.IsRead)
        {
            notification.MarkAsRead();
            await _context.SaveChangesAsync(cancellationToken);
        }
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
        var unread = await _context.Notifications
            .Where(x => x.UserId == request.UserId && !x.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in unread)
        {
            notification.MarkAsRead();
        }

        await _context.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }
}
