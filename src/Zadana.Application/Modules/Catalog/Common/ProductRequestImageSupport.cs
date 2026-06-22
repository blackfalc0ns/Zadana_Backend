using System.Text.Json;

namespace Zadana.Application.Modules.Catalog.Common;

public static class ProductRequestImageSupport
{
    public static string? SerializeImageUrls(IReadOnlyCollection<string>? imageUrls)
    {
        var normalized = imageUrls?
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized is null || normalized.Count == 0
            ? null
            : JsonSerializer.Serialize(normalized);
    }

    public static IReadOnlyList<string> ParseImageUrls(string? imageUrlsJson, string? primaryImageUrl = null)
    {
        var urls = new List<string>();

        if (!string.IsNullOrWhiteSpace(imageUrlsJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(imageUrlsJson);
                if (parsed is not null)
                {
                    urls.AddRange(parsed.Where(url => !string.IsNullOrWhiteSpace(url)).Select(url => url.Trim()));
                }
            }
            catch (JsonException)
            {
                // Fall back to the primary image when legacy or malformed payloads are encountered.
            }
        }

        if (!string.IsNullOrWhiteSpace(primaryImageUrl)
            && !urls.Any(url => string.Equals(url, primaryImageUrl.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            urls.Insert(0, primaryImageUrl.Trim());
        }

        return urls
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
