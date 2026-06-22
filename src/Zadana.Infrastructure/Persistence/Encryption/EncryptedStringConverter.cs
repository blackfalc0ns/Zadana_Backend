using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Security.Cryptography;
using System.Text;

namespace Zadana.Infrastructure.Persistence.Encryption;

/// <summary>
/// Encrypts PII strings with a stable AES-GCM key. The v2 format is portable
/// across application restarts and worker processes. Legacy DataProtection
/// v1 values remain readable while their original key ring is available.
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string?, string?>
{
    private const string LegacyPrefix = "enc:v1:";
    private const string Prefix = "enc:v2:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public EncryptedStringConverter(byte[] encryptionKey, IDataProtector? legacyProtector = null)
        : base(
            plain => Encrypt(plain, encryptionKey),
            stored => Decrypt(stored, encryptionKey, legacyProtector))
    {
    }

    private static string? Encrypt(string? plain, byte[] encryptionKey)
    {
        if (string.IsNullOrEmpty(plain))
        {
            return plain;
        }

        if (plain.StartsWith(Prefix, StringComparison.Ordinal) ||
            plain.StartsWith(LegacyPrefix, StringComparison.Ordinal))
        {
            return plain;
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintext = Encoding.UTF8.GetBytes(plain);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(encryptionKey, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize + TagSize, ciphertext.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    private static string? Decrypt(
        string? stored,
        byte[] encryptionKey,
        IDataProtector? legacyProtector)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return stored;
        }

        if (stored.StartsWith(LegacyPrefix, StringComparison.Ordinal))
        {
            if (legacyProtector is null)
            {
                return null;
            }

            try
            {
                return legacyProtector.Unprotect(stored[LegacyPrefix.Length..]);
            }
            catch
            {
                return null;
            }
        }

        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return stored;
        }

        try
        {
            var payload = Convert.FromBase64String(stored[Prefix.Length..]);
            if (payload.Length <= NonceSize + TagSize)
            {
                return null;
            }

            var nonce = payload.AsSpan(0, NonceSize);
            var tag = payload.AsSpan(NonceSize, TagSize);
            var ciphertext = payload.AsSpan(NonceSize + TagSize);
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(encryptionKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch
        {
            return null;
        }
    }
}

public static class PiiProtector
{
    public const string LegacyPurpose = "Zadana.Pii.v1";

    public static EncryptedStringConverter CreateConverter(
        byte[] masterKey,
        IDataProtectionProvider? legacyProvider = null)
    {
        var encryptionKey = HMACSHA256.HashData(
            masterKey,
            Encoding.UTF8.GetBytes("Zadana.Pii.v2"));
        var legacyProtector = legacyProvider?.CreateProtector(LegacyPurpose);
        return new EncryptedStringConverter(encryptionKey, legacyProtector);
    }
}
