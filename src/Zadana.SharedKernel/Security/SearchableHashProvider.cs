using System.Security.Cryptography;
using System.Text;

namespace Zadana.SharedKernel.Security;

/// <summary>
/// Generates deterministic HMAC-SHA256 hashes for encrypted values that need
/// equality lookups without exposing their plaintext.
/// </summary>
public static class SearchableHashProvider
{
    private static byte[]? _key;

    public static void Configure(byte[] key)
    {
        if (key is null || key.Length < 32)
        {
            throw new ArgumentException("Searchable hash key must be at least 32 bytes.", nameof(key));
        }

        _key = key;
    }

    public static string? Compute(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || _key is null)
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(value.Trim());
        using var hmac = new HMACSHA256(_key);
        return Convert.ToHexString(hmac.ComputeHash(bytes)).ToLowerInvariant();
    }
}
