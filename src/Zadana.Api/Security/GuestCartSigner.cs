using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Zadana.Api.Security;

/// <summary>
/// Lightweight HMAC signer for the X-Device-Id header used by guest cart
/// flows. Without a signature, an attacker who guesses another visitor's
/// device id can mutate that visitor's cart. With this signer, the mobile
/// app obtains a deterministic signature on first use and includes it in
/// every cart call; the server verifies the signature in O(1) without DB.
/// </summary>
public sealed class GuestCartSigner
{
    public const string DeviceHeader = "X-Device-Id";
    public const string SignatureHeader = "X-Device-Signature";

    private readonly byte[] _signingKey;

    public GuestCartSigner(IConfiguration configuration)
    {
        var secret = configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JWT Secret is not configured.");
        _signingKey = Encoding.UTF8.GetBytes(secret);
    }

    public string Sign(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Device id is required", nameof(deviceId));
        }

        using var hmac = new HMACSHA256(_signingKey);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes("guest-cart|" + deviceId.Trim()));
        return Base64UrlEncode(bytes);
    }

    public bool Verify(string? deviceId, string? signature)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var expected = Sign(deviceId);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(signature.Trim());
        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
