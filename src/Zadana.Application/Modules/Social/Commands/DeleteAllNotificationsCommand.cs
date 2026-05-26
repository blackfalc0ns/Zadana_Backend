using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Application.Modules.Social.Commands;

public record DeleteAllNotificationsCommand(Guid UserId) : IRequest<int>;

public class DeleteAllNotificationsCommandHandler : IRequestHandler<DeleteAllNotificationsCommand, int>
{
    private readonly IApplicationDbContext _context;

    public DeleteAllNotificationsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(DeleteAllNotificationsCommand request, CancellationToken cancellationToken)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == request.UserId)
            .ToListAsync(cancellationToken);

        _context.Notifications.RemoveRange(notifications);
        await _context.SaveChangesAsync(cancellationToken);
        return notifications.Count;
    }
}
