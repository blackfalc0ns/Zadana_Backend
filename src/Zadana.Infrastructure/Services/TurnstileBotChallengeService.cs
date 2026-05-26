using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Infrastructure.Services;

/// <summary>
/// Cloudflare Turnstile implementation of <see cref="IBotChallengeService"/>.
/// Set BotChallenge:SecretKey in environment variables to enable; when the
/// secret is absent the service returns success so the rest of the system
/// keeps working (useful for local dev / staging without a real Cloudflare
/// site key).
/// </summary>
public sealed class TurnstileBotChallengeService : IBotChallengeService
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TurnstileBotChallengeService> _logger;

    public TurnstileBotChallengeService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TurnstileBotChallengeService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["BotChallenge:SecretKey"]);

    public async Task<BotChallengeResult> VerifyAsync(
        string? token,
        string? remoteIp,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            // Provider not enabled; allow the request to proceed.
            return new BotChallengeResult(true, null);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new BotChallengeResult(false, "MISSING_TOKEN");
        }

        var payload = new Dictionary<string, string>
        {
            ["secret"] = _configuration["BotChallenge:SecretKey"]!,
            ["response"] = token.Trim()
        };
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            payload["remoteip"] = remoteIp;
        }

        try
        {
            using var content = new FormUrlEncodedContent(payload);
            using var resp = await _httpClient.PostAsync(VerifyUrl, content, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile verify returned HTTP {Status}.", resp.StatusCode);
                return new BotChallengeResult(false, "VERIFY_HTTP_ERROR");
            }

            var body = await resp.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: cancellationToken);
            if (body is null)
            {
                return new BotChallengeResult(false, "VERIFY_EMPTY_RESPONSE");
            }

            if (!body.Success)
            {
                var reason = body.ErrorCodes is { Length: > 0 } ? string.Join(',', body.ErrorCodes) : "INVALID_TOKEN";
                return new BotChallengeResult(false, reason);
            }

            return new BotChallengeResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Turnstile verification failed unexpectedly.");
            // Fail closed: do not let an attacker bypass by exhausting our
            // outbound connections.
            return new BotChallengeResult(false, "VERIFY_EXCEPTION");
        }
    }

    private sealed record TurnstileResponse(
        bool Success,
        string[]? ErrorCodes,
        string? Action,
        string? Cdata);
}
