using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Zadana.Application.Common.Caching;

public static class AppCacheKeys
{
    public static string CurrentCulture => NormalizeToken(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static string Build(params string[] segments) =>
        string.Join(':', segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));

    public static string GuidToken(Guid? value) => value?.ToString("N") ?? "none";

    public static string IntToken(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string DecimalToken(decimal? value) =>
        value.HasValue
            ? value.Value.ToString("0.####", CultureInfo.InvariantCulture)
            : "none";

    public static string BoolToken(bool value) => value ? "1" : "0";

    public static string EnumToken<TEnum>(TEnum? value) where TEnum : struct, Enum =>
        value.HasValue ? NormalizeToken(value.Value.ToString()) : "none";

    public static string TextToken(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "none"
            : HashToken(value.Trim().ToLowerInvariant());

    public static string ScopeToken(Guid? userId, string? guestDeviceId)
    {
        if (userId.HasValue)
        {
            return $"user:{userId.Value:N}";
        }

        if (!string.IsNullOrWhiteSpace(guestDeviceId))
        {
            return $"guest:{HashToken(guestDeviceId.Trim())}";
        }

        return "anonymous";
    }

    public static string FavoriteScopeTag(Guid? userId, string? guestDeviceId) =>
        $"favorites:{ScopeToken(userId, guestDeviceId)}";

    public static string PurchaseProfileTag(Guid userId) => $"purchase-profile:user:{userId:N}";

    public static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        var trimmed = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(trimmed.Length);
        var previousWasSeparator = false;

        foreach (var character in trimmed)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        return builder
            .ToString()
            .Trim('-') is { Length: > 0 } token
            ? token
            : "none";
    }

    private static string HashToken(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
