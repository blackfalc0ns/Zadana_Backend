namespace Zadana.Application.Common.Interfaces;

/// <summary>
/// Verifies a CAPTCHA / bot-challenge token. The default Cloudflare
/// Turnstile implementation talks to Cloudflare's siteverify endpoint;
/// other providers (hCaptcha, reCAPTCHA) can be plugged in by replacing
/// the registration in DI.
/// </summary>
public interface IBotChallengeService
{
    bool IsConfigured { get; }

    /// <summary>
    /// Returns true when the token is valid for the given remote IP.
    /// When the provider is not configured (e.g. local dev), returns true
    /// to keep flows usable.
    /// </summary>
    Task<BotChallengeResult> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken = default);
}

public sealed record BotChallengeResult(bool Success, string? FailureReason);
