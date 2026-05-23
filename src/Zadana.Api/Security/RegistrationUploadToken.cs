using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Zadana.Api.Security;

/// <summary>
/// Short-lived HMAC-signed token used to authorize anonymous file uploads
/// during driver/vendor registration before the user has a real JWT.
///
/// The token is a single string with three parts separated by '.':
///     base64Url(payload).base64Url(hmac)
/// Where payload is "{purpose}|{deviceId}|{expiresUnixUtc}".
///
/// HMAC is signed with the JWT secret so we don't introduce a new key.
/// </summary>
public sealed class RegistrationUploadTokenService
{
    public const string HeaderName = "X-Registration-Upload-Token";
    public const string PurposeRegistration = "registration_upload";
    private const int LifetimeMinutes = 15;

    private readonly byte[] _signingKey;

    public RegistrationUploadTokenService(IConfiguration configuration)
    {
        var secret = configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JWT Secret is not configured.");
        _signingKey = Encoding.UTF8.GetBytes(secret);
    }

    public RegistrationUploadTokenIssued Issue(string purpose, string deviceId)
    {
        var purposeNorm = string.IsNullOrWhiteSpace(purpose) ? PurposeRegistration : purpose.Trim();
        var deviceNorm = string.IsNullOrWhiteSpace(deviceId) ? "anonymous" : deviceId.Trim();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(LifetimeMinutes);
        var payload = $"{purposeNorm}|{deviceNorm}|{expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}";
        var token = Sign(payload);
        return new RegistrationUploadTokenIssued(token, expiresAt);
    }

    public bool TryValidate(string? token, string requiredPurpose, out string? failureReason)
    {
        failureReason = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            failureReason = "TOKEN_MISSING";
            return false;
        }

        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            failureReason = "TOKEN_MALFORMED";
            return false;
        }

        string payload;
        byte[] expectedSignature;
        try
        {
            payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            expectedSignature = Base64UrlDecode(parts[1]);
        }
        catch
        {
            failureReason = "TOKEN_MALFORMED";
            return false;
        }

        var actualSignature = ComputeSignature(payload);
        if (expectedSignature.Length != actualSignature.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
        {
            failureReason = "TOKEN_INVALID_SIGNATURE";
            return false;
        }

        var segments = payload.Split('|');
        if (segments.Length != 3)
        {
            failureReason = "TOKEN_MALFORMED_PAYLOAD";
            return false;
        }

        if (!string.Equals(segments[0], requiredPurpose, StringComparison.Ordinal))
        {
            failureReason = "TOKEN_WRONG_PURPOSE";
            return false;
        }

        if (!long.TryParse(segments[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresUnix))
        {
            failureReason = "TOKEN_INVALID_EXPIRY";
            return false;
        }

        if (DateTimeOffset.FromUnixTimeSeconds(expiresUnix) <= DateTimeOffset.UtcNow)
        {
            failureReason = "TOKEN_EXPIRED";
            return false;
        }

        return true;
    }

    private string Sign(string payload)
    {
        var sig = ComputeSignature(payload);
        return Base64UrlEncode(Encoding.UTF8.GetBytes(payload)) + "." + Base64UrlEncode(sig);
    }

    private byte[] ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(_signingKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}

public sealed record RegistrationUploadTokenIssued(string Token, DateTimeOffset ExpiresAtUtc);
