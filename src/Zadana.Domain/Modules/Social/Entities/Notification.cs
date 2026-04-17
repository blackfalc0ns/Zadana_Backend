using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Social.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TitleAr { get; private set; } = null!;
    public string TitleEn { get; private set; } = null!;
    public string BodyAr { get; private set; } = null!;
    public string BodyEn { get; private set; } = null!;
    public string? Type { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Data { get; private set; }
    public bool IsRead { get; private set; }

    // Legacy compatibility
    public string Title => TitleAr;
    public string Body => BodyAr;

    // Navigation
    public User User { get; private set; } = null!;

    private Notification() { }

    public Notification(
        Guid userId,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type = null,
        Guid? referenceId = null,
        string? data = null)
    {
        UserId = userId;
        TitleAr = titleAr.Trim();
        TitleEn = titleEn.Trim();
        BodyAr = bodyAr.Trim();
        BodyEn = bodyEn.Trim();
        Type = type?.Trim();
        ReferenceId = referenceId;
        Data = data;
        IsRead = false;
    }

    public void MarkAsRead() => IsRead = true;
}
