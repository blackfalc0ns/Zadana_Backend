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
    /// <summary>
    /// Configure the HMAC key. Must be called once at startup before any
    /// entity that depends on hashed columns is materialised. Keys shorter
    /// than 32 bytes are rejected so callers cannot accidentally deploy a
    /// weak key in production.
    /// </summary>
    public static void Configure(byte[] key) =>
        Zadana.SharedKernel.Security.SearchableHashProvider.Configure(key);

    /// <summary>
    /// Returns the lowercase hex HMAC of <paramref name="value"/> trimmed of
    /// whitespace. Returns null when the value is null/empty so EF Core can
    /// store SQL NULL and the unique-with-filter index keeps working.
    /// </summary>
    public static string? Compute(string? value) =>
        Zadana.SharedKernel.Security.SearchableHashProvider.Compute(value);
}
