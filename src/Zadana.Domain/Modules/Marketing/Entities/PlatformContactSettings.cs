using System.Net.Mail;
using System.Text.RegularExpressions;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Marketing.Entities;

/// <summary>
/// Singleton platform contact channels shown to customers (support + social).
/// </summary>
public sealed class PlatformContactSettings : BaseEntity
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    public string? SupportEmail { get; private set; }
    public string? SupportPhone { get; private set; }
    public string? WhatsAppUrl { get; private set; }
    public string? InstagramUrl { get; private set; }
    public string? TwitterUrl { get; private set; }
    public string? TikTokUrl { get; private set; }
    public string? SnapchatUrl { get; private set; }
    public string? FacebookUrl { get; private set; }
    public string? YouTubeUrl { get; private set; }
    public string? LinkedInUrl { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private PlatformContactSettings()
    {
    }

    public PlatformContactSettings(Guid? updatedByUserId = null)
    {
        Id = SingletonId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(
        string? supportEmail,
        string? supportPhone,
        string? whatsAppUrl,
        string? instagramUrl,
        string? twitterUrl,
        string? tikTokUrl,
        string? snapchatUrl,
        string? facebookUrl,
        string? youTubeUrl,
        string? linkedInUrl,
        Guid? updatedByUserId)
    {
        SupportEmail = NormalizeEmail(supportEmail);
        SupportPhone = NormalizePhone(supportPhone);
        WhatsAppUrl = NormalizeUrl(whatsAppUrl, "WHATSAPP_URL_INVALID");
        InstagramUrl = NormalizeUrl(instagramUrl, "INSTAGRAM_URL_INVALID");
        TwitterUrl = NormalizeUrl(twitterUrl, "TWITTER_URL_INVALID");
        TikTokUrl = NormalizeUrl(tikTokUrl, "TIKTOK_URL_INVALID");
        SnapchatUrl = NormalizeUrl(snapchatUrl, "SNAPCHAT_URL_INVALID");
        FacebookUrl = NormalizeUrl(facebookUrl, "FACEBOOK_URL_INVALID");
        YouTubeUrl = NormalizeUrl(youTubeUrl, "YOUTUBE_URL_INVALID");
        LinkedInUrl = NormalizeUrl(linkedInUrl, "LINKEDIN_URL_INVALID");
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var email = value.Trim();
        try
        {
            _ = new MailAddress(email);
        }
        catch
        {
            throw new BusinessRuleException("SUPPORT_EMAIL_INVALID", "Support email is invalid.");
        }

        return email;
    }

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var phone = value.Trim();
        if (!Regex.IsMatch(phone, @"^\+?[0-9\s\-()]{7,20}$"))
        {
            throw new BusinessRuleException("SUPPORT_PHONE_INVALID", "Support phone is invalid.");
        }

        return phone;
    }

    private static string? NormalizeUrl(string? value, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var url = value.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessRuleException(errorCode, "Social URL must be an absolute http(s) link.");
        }

        return uri.AbsoluteUri;
    }
}
