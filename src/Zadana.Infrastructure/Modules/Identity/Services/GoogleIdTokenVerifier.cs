using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Modules.Identity.Services;

public sealed class GoogleIdTokenVerifier : IGoogleIdTokenVerifier
{
    private readonly IConfiguration _configuration;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<GoogleIdTokenVerifier> _logger;

    public GoogleIdTokenVerifier(
        IConfiguration configuration,
        IStringLocalizer<SharedResource> localizer,
        ILogger<GoogleIdTokenVerifier> logger)
    {
        _configuration = configuration;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<GoogleIdTokenProfile> VerifyAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new BusinessRuleException("GOOGLE_TOKEN_REQUIRED", _localizer["InvalidCredentials"]);
        }

        var clientId = _configuration["GoogleAuth:ClientId"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId) ||
            clientId.StartsWith("__SET_VIA_ENV__", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("GOOGLE_AUTH_NOT_CONFIGURED", "Google sign-in is not configured.");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [clientId]
                });
        }
        catch (Exception ex) when (ex is InvalidJwtException || ex.GetType().Name.Contains("Jwt", StringComparison.OrdinalIgnoreCase))
        {
            var tokenAudience = TryReadUnverifiedAudience(idToken);
            _logger.LogWarning(
                ex,
                "Google ID token validation failed. ConfiguredAudience={ConfiguredAudience} TokenAudience={TokenAudience} ErrorType={ErrorType}",
                clientId,
                tokenAudience ?? "(unreadable)",
                ex.GetType().Name);

            throw new UnauthorizedException(_localizer["InvalidCredentials"], "GOOGLE_TOKEN_INVALID");
        }
        catch (Exception ex)
        {
            var tokenAudience = TryReadUnverifiedAudience(idToken);
            _logger.LogError(
                ex,
                "Unexpected Google ID token validation error. ConfiguredAudience={ConfiguredAudience} TokenAudience={TokenAudience}",
                clientId,
                tokenAudience ?? "(unreadable)");

            throw new UnauthorizedException(_localizer["InvalidCredentials"], "GOOGLE_TOKEN_INVALID");
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new BusinessRuleException("GOOGLE_EMAIL_REQUIRED", _localizer["InvalidEmail"]);
        }

        if (!payload.EmailVerified)
        {
            throw new BusinessRuleException("GOOGLE_EMAIL_NOT_VERIFIED", _localizer["AccountEmailNotVerified"]);
        }

        var givenName = payload.GivenName?.Trim();
        var familyName = payload.FamilyName?.Trim();
        var fullName = !string.IsNullOrWhiteSpace(payload.Name)
            ? payload.Name.Trim()
            : $"{givenName} {familyName}".Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = payload.Email.Split('@')[0];
        }

        return new GoogleIdTokenProfile(
            payload.Subject,
            payload.Email.Trim(),
            payload.EmailVerified,
            fullName,
            string.IsNullOrWhiteSpace(givenName) ? null : givenName,
            string.IsNullOrWhiteSpace(familyName) ? null : familyName,
            string.IsNullOrWhiteSpace(payload.Picture) ? null : payload.Picture);
    }

    private static string? TryReadUnverifiedAudience(string idToken)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
            if (jwt.Audiences?.Any() == true)
            {
                return string.Join(',', jwt.Audiences);
            }

            if (jwt.Payload.TryGetValue("aud", out var aud))
            {
                return aud switch
                {
                    string s => s,
                    JsonElement { ValueKind: JsonValueKind.String } el => el.GetString(),
                    JsonElement { ValueKind: JsonValueKind.Array } arr => string.Join(',',
                        arr.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x))),
                    _ => aud?.ToString()
                };
            }
        }
        catch
        {
            // Best-effort diagnostics only.
        }

        return null;
    }
}
