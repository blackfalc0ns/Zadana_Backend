using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Marketing.Entities;

public class HomeBanner : BaseEntity
{
    public string TagAr { get; private set; } = null!;
    public string TagEn { get; private set; } = null!;
    public string TitleAr { get; private set; } = null!;
    public string TitleEn { get; private set; } = null!;
    public string? SubtitleAr { get; private set; }
    public string? SubtitleEn { get; private set; }
    public string? ActionLabelAr { get; private set; }
    public string? ActionLabelEn { get; private set; }
    public string ImageUrl { get; private set; } = null!;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }

    private HomeBanner() { }

    public HomeBanner(
        string tagAr,
        string tagEn,
        string titleAr,
        string titleEn,
        string imageUrl,
        string? subtitleAr = null,
        string? subtitleEn = null,
        string? actionLabelAr = null,
        string? actionLabelEn = null,
        int displayOrder = 0,
        DateTime? startsAtUtc = null,
        DateTime? endsAtUtc = null)
    {
        TagAr = tagAr.Trim();
        TagEn = tagEn.Trim();
        TitleAr = titleAr.Trim();
        TitleEn = titleEn.Trim();
        SubtitleAr = subtitleAr?.Trim();
        SubtitleEn = subtitleEn?.Trim();
        ActionLabelAr = actionLabelAr?.Trim();
        ActionLabelEn = actionLabelEn?.Trim();
        ImageUrl = imageUrl.Trim();
        DisplayOrder = displayOrder;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        IsActive = true;
    }

    public void UpdateContent(
        string tagAr,
        string tagEn,
        string titleAr,
        string titleEn,
        string imageUrl,
        string? subtitleAr,
        string? subtitleEn,
        string? actionLabelAr,
        string? actionLabelEn,
        int displayOrder,
        DateTime? startsAtUtc,
        DateTime? endsAtUtc)
    {
        TagAr = tagAr.Trim();
        TagEn = tagEn.Trim();
        TitleAr = titleAr.Trim();
        TitleEn = titleEn.Trim();
        SubtitleAr = subtitleAr?.Trim();
        SubtitleEn = subtitleEn?.Trim();
        ActionLabelAr = actionLabelAr?.Trim();
        ActionLabelEn = actionLabelEn?.Trim();
        ImageUrl = imageUrl.Trim();
        DisplayOrder = displayOrder;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
