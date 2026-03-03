using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Social.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public string? Type { get; private set; }
    public bool IsRead { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;

    private Notification() { }

    public Notification(Guid userId, string title, string body, string? type = null)
    {
        UserId = userId;
        Title = title.Trim();
        Body = body.Trim();
        Type = type?.Trim();
        IsRead = false;
    }

    public void MarkAsRead() => IsRead = true;
}
