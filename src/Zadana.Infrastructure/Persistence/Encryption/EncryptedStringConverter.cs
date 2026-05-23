using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Zadana.Infrastructure.Persistence.Encryption;

/// <summary>
/// EF Core value converter that transparently encrypts string columns at
/// rest using ASP.NET Core's <see cref="IDataProtector"/>.
///
/// Storage format on disk: "enc:v1:{base64-cipher}".
/// Plaintext rows already in the database remain readable: anything that
/// does not start with the "enc:v1:" prefix is returned unchanged. New
/// writes always encrypt. Run a one-off migration job to re-save legacy
/// rows once everything is verified, then enforce strict mode.
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string?, string?>
{
    private const string Prefix = "enc:v1:";

    public EncryptedStringConverter(IDataProtector protector)
        : base(
            plain => Encrypt(plain, protector),
            stored => Decrypt(stored, protector))
    {
    }

    private static string? Encrypt(string? plain, IDataProtector protector)
    {
        if (string.IsNullOrEmpty(plain))
        {
            return plain;
        }

        // If the value already looks encrypted (e.g., we round-tripped through
        // the converter twice), do not double-encrypt.
        if (plain.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return plain;
        }

        var cipher = protector.Protect(plain);
        return Prefix + cipher;
    }

    private static string? Decrypt(string? stored, IDataProtector protector)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return stored;
        }

        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // Legacy plaintext row — return as-is so existing data stays
            // readable. Will be re-encrypted on next write.
            return stored;
        }

        try
        {
            return protector.Unprotect(stored[Prefix.Length..]);
        }
        catch
        {
            // Corrupt or wrong key. Fail closed: surface as null so callers
            // see "no data" instead of a crash.
            return null;
        }
    }
}

/// <summary>
/// Builds the application-wide <see cref="EncryptedStringConverter"/> bound
/// to a stable purpose so re-deployments don't lose the ability to decrypt
/// existing rows (DataProtection keys are persisted to disk).
/// </summary>
public static class PiiProtector
{
    public const string Purpose = "Zadana.Pii.v1";

    public static EncryptedStringConverter CreateConverter(IDataProtectionProvider provider)
    {
        var protector = provider.CreateProtector(Purpose);
        return new EncryptedStringConverter(protector);
    }
}
