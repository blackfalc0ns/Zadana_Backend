using System.Text.Json;

namespace Zadana.Application.Modules.Social.Support;

public static class NotificationPayloadHelper
{
    private const int TitleMaxLength = 200;
    private const int BodyMaxLength = 1000;
    private const int DataMaxLength = 4000;
    private const int TruncatedExcerptLength = 3000;

    public static SanitizedNotificationPayload Sanitize(
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? type,
        string? data)
    {
        var normalizedData = NormalizeData(data);
        var normalizedTitleAr = NormalizeText(titleAr, TitleMaxLength);
        var normalizedTitleEn = NormalizeText(titleEn, TitleMaxLength);
        var normalizedBodyAr = NormalizeText(bodyAr, BodyMaxLength);
        var normalizedBodyEn = NormalizeText(bodyEn, BodyMaxLength);

        return new SanitizedNotificationPayload(
            ResolveLocalizedText(normalizedTitleAr, normalizedTitleEn),
            ResolveLocalizedText(normalizedTitleEn, normalizedTitleAr),
            ResolveLocalizedText(normalizedBodyAr, normalizedBodyEn),
            ResolveLocalizedText(normalizedBodyEn, normalizedBodyAr),
            NormalizeOptionalText(type, 100),
            normalizedData,
            TryParseData(normalizedData));
    }

    public static JsonElement? TryParseData(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(data);
            return document.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeText(string value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..maxLength].TrimEnd();
    }

    private static string ResolveLocalizedText(string primary, string fallback)
    {
        var normalizedPrimary = primary.Trim();
        var normalizedFallback = fallback.Trim();

        if (LooksCorrupted(normalizedPrimary) && !string.IsNullOrWhiteSpace(normalizedFallback))
        {
            return normalizedFallback;
        }

        if (string.IsNullOrWhiteSpace(normalizedPrimary))
        {
            return normalizedFallback;
        }

        return normalizedPrimary;
    }

    private static bool LooksCorrupted(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.Contains("Ø", StringComparison.Ordinal) ||
            value.Contains("Ù", StringComparison.Ordinal) ||
            value.Contains("â", StringComparison.Ordinal))
        {
            return true;
        }

        var questionMarkCount = value.Count(ch => ch == '?');
        return questionMarkCount >= 3 || questionMarkCount >= Math.Max(3, value.Length / 4);
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeText(value, maxLength);
    }

    private static string? NormalizeData(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        var normalized = data.Trim();

        try
        {
            using var document = JsonDocument.Parse(normalized);
            normalized = JsonSerializer.Serialize(document.RootElement);
        }
        catch
        {
            // Keep non-JSON payloads as-is for backward compatibility.
        }

        if (normalized.Length <= DataMaxLength)
        {
            return normalized;
        }

        return JsonSerializer.Serialize(new
        {
            truncated = true,
            originalLength = normalized.Length,
            excerpt = normalized[..Math.Min(normalized.Length, TruncatedExcerptLength)]
        });
    }
}

public sealed record SanitizedNotificationPayload(
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string? Type,
    string? Data,
    JsonElement? DataObject);
