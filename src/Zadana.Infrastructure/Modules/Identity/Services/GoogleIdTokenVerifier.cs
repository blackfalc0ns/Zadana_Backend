using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Modules.Identity.Services;

public sealed class GoogleIdTokenVerifier : IGoogleIdTokenVerifier
{
    private readonly IConfiguration _configuration;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public GoogleIdTokenVerifier(
        IConfiguration configuration,
        IStringLocalizer<SharedResource> localizer)
    {
        _configuration = configuration;
        _localizer = localizer;
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
        catch (InvalidJwtException)
        {
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
}
