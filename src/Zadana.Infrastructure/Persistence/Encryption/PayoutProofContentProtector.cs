using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Zadana.Infrastructure.Persistence.Encryption;

/// <summary>
/// Protects payout proof bytes with a stable AES-GCM key derived from the
/// platform master key. Legacy Data Protection payloads remain readable when
/// the original key ring is still available (including revoked keys).
/// </summary>
public sealed class PayoutProofContentProtector
{
    private static readonly byte[] VersionPrefix = "ZP2\0"u8.ToArray();
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string LegacyPurpose = "Zadana.PayoutProofAttachment.v1";

    private readonly byte[] _aesKey;
    private readonly IDataProtector _legacyProtector;
    private readonly IPersistedDataProtector? _persistedProtector;

    public PayoutProofContentProtector(byte[] masterKey, IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        if (masterKey.Length < 16)
        {
            throw new ArgumentException("A master key of at least 16 bytes is required.", nameof(masterKey));
        }

        _aesKey = HMACSHA256.HashData(masterKey, Encoding.UTF8.GetBytes("Zadana.PayoutProof.v2"));
        _legacyProtector = dataProtectionProvider.CreateProtector(LegacyPurpose);
        _persistedProtector = _legacyProtector as IPersistedDataProtector;
    }

    public byte[] Protect(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            throw new ArgumentException("Proof content cannot be empty.", nameof(content));
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[content.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_aesKey, TagSize);
        aes.Encrypt(nonce, content, ciphertext, tag);

        var payload = new byte[VersionPrefix.Length + NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(VersionPrefix, 0, payload, 0, VersionPrefix.Length);
        Buffer.BlockCopy(nonce, 0, payload, VersionPrefix.Length, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, VersionPrefix.Length + NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, VersionPrefix.Length + NonceSize + TagSize, ciphertext.Length);
        return payload;
    }

    public byte[] Unprotect(byte[] protectedContent)
    {
        ArgumentNullException.ThrowIfNull(protectedContent);
        if (protectedContent.Length == 0)
        {
            throw new CryptographicException("Protected proof content is empty.");
        }

        if (IsV2(protectedContent))
        {
            return UnprotectV2(protectedContent);
        }

        try
        {
            return _legacyProtector.Unprotect(protectedContent);
        }
        catch (CryptographicException) when (_persistedProtector is not null)
        {
            // Persisted finance proofs must remain readable even if the DP key
            // that sealed them was later revoked during key-ring rotation.
            return _persistedProtector.DangerousUnprotect(
                protectedContent,
                ignoreRevocationErrors: true,
                out _,
                out _);
        }
    }

    private static bool IsV2(byte[] protectedContent)
    {
        if (protectedContent.Length < VersionPrefix.Length)
        {
            return false;
        }

        for (var i = 0; i < VersionPrefix.Length; i++)
        {
            if (protectedContent[i] != VersionPrefix[i])
            {
                return false;
            }
        }

        return true;
    }

    private byte[] UnprotectV2(byte[] protectedContent)
    {
        var headerLength = VersionPrefix.Length + NonceSize + TagSize;
        if (protectedContent.Length <= headerLength)
        {
            throw new CryptographicException("Protected proof payload is truncated.");
        }

        var nonce = protectedContent.AsSpan(VersionPrefix.Length, NonceSize);
        var tag = protectedContent.AsSpan(VersionPrefix.Length + NonceSize, TagSize);
        var ciphertext = protectedContent.AsSpan(headerLength);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_aesKey, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
