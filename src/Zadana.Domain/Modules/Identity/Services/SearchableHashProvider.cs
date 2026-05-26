using System.Security.Cryptography;
using System.Text;

namespace Zadana.Domain.Modules.Identity.Services;

/// <summary>
/// Generates a deterministic HMAC-SHA256 hash for a sensitive plaintext value
/// (national id, IBAN, etc.) so the value can be indexed and looked up by
/// equality/prefix without exposing the plaintext or breaking column-level
/// encryption applied by the data-protection layer.
/// 
/// The HMAC key is set once at application startup via <see cref="Configure"/>
/// and shared across the process. It is intentionally separate from the
/// data-protection encryption key — losing one does not compromise the other.
/// </summary>
public static class SearchableHashProvider
{
    private static byte[]? _key;

    /// <summary>
    /// Configure the HMAC key. Must be called once at startup before any
    /// entity that depends on hashed columns is materialised. Keys shorter
    /// than 32 bytes are rejected so callers cannot accidentally deploy a
    /// weak key in production.
    /// </summary>
    public static void Configure(byte[] key)
    {
        if (key is null || key.Length < 32)
        {
            throw new ArgumentException(
                "Searchable hash key must be at least 32 bytes.", nameof(key));
        }
        _key = key;
    }

    /// <summary>
    /// Returns the lowercase hex HMAC of <paramref name="value"/> trimmed of
    /// whitespace. Returns null when the value is null/empty so EF Core can
    /// store SQL NULL and the unique-with-filter index keeps working.
    /// </summary>
    public static string? Compute(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (_key is null)
        {
            // Configure() not called (tests / design-time tooling). Skip
            // hashing rather than throw so seed / model-build paths keep
            // working. Production startup wires the key explicitly.
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(value.Trim());
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
