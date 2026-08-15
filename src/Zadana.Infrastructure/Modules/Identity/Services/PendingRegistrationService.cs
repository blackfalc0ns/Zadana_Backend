using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Support;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Infrastructure.Modules.Identity.Services;

/// <summary>
/// Holds pending signup state in a short-lived signed JWT plus a distributed cache lookup
/// keyed by platform + email/phone, so verify/resend work even when the client omits the token.
/// </summary>
public sealed class PendingRegistrationService : IPendingRegistrationService
{
    public const string TokenUseClaim = "token_use";
    public const string TokenUseValue = "registration";
    public const string SessionClaim = "reg_session";

    private const string CachePrefix = "preg:";

    private static readonly UserRole[] LookupRoles =
    [
        UserRole.Customer,
        UserRole.Vendor,
        UserRole.Driver
    ];

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly IIdentityAccountService _identityAccountService;
    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;

    public PendingRegistrationService(
        IIdentityAccountService identityAccountService,
        UserManager<User> userManager,
        IConfiguration configuration,
        IDistributedCache cache)
    {
        _identityAccountService = identityAccountService;
        _userManager = userManager;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<PendingRegistrationStartResult> StartAsync(
        StartPendingRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber?.Trim();

        var existingUserId = await ResolveLinkableAccountAsync(email, phone, request.Role, cancellationToken);
        if (existingUserId == Guid.Empty)
        {
            return new PendingRegistrationStartResult(PendingRegistrationStartStatus.DuplicateEmailOrPhone);
        }

        string? linkedOtpEmail = null;
        if (existingUserId.HasValue)
        {
            var existing = await _identityAccountService.FindByIdAsync(existingUserId.Value, cancellationToken);
            linkedOtpEmail = existing?.Email;
        }

        var pending = new PendingRegistration(
            email,
            phone,
            HashPassword(request.Password),
            request.FullName,
            request.Role,
            request.PayloadJson,
            request.ProfilePhotoUrl,
            existingUserId,
            linkedOtpEmail);

        var otp = pending.GenerateOtp();
        var token = CreateToken(pending);
        await PersistSessionAsync(pending, cancellationToken);

        return new PendingRegistrationStartResult(
            PendingRegistrationStartStatus.Succeeded,
            Map(pending),
            otp,
            token);
    }

    public async Task<PendingOtpDispatchResult> ResendOtpAsync(
        string? registrationToken,
        UserRole? expectedRole = null,
        string? identifier = null,
        CancellationToken cancellationToken = default)
    {
        var pending = await ResolveSessionAsync(registrationToken, identifier, expectedRole, cancellationToken);
        if (pending is null)
        {
            return new PendingOtpDispatchResult(PendingOtpDispatchStatus.NotFound);
        }

        if (pending.IsExpired())
        {
            return new PendingOtpDispatchResult(PendingOtpDispatchStatus.Expired);
        }

        if (!pending.CanResendOtp())
        {
            var currentToken = CreateToken(pending);
            await PersistSessionAsync(pending, cancellationToken);
            return new PendingOtpDispatchResult(
                PendingOtpDispatchStatus.CooldownActive,
                Map(pending),
                RegistrationToken: currentToken,
                CooldownSecondsRemaining: pending.ResendCooldownSecondsRemaining());
        }

        var otp = pending.GenerateOtp();
        var token = CreateToken(pending);
        await PersistSessionAsync(pending, cancellationToken);
        return new PendingOtpDispatchResult(
            PendingOtpDispatchStatus.Succeeded,
            Map(pending),
            otp,
            token);
    }

    public async Task<PendingCompletionResult> VerifyAndCreateAccountAsync(
        string? registrationToken,
        string otpCode,
        string identifier,
        UserRole? expectedRole = null,
        CancellationToken cancellationToken = default)
    {
        var pending = await ResolveSessionAsync(registrationToken, identifier, expectedRole, cancellationToken);
        if (pending is null)
        {
            return new PendingCompletionResult(PendingCompletionStatus.NotFound);
        }

        if (pending.IsExpired())
        {
            return new PendingCompletionResult(PendingCompletionStatus.Expired);
        }

        if (!pending.VerifyOtp(otpCode))
        {
            var rotated = CreateToken(pending);
            await PersistSessionAsync(pending, cancellationToken);
            return new PendingCompletionResult(
                PendingCompletionStatus.InvalidOtp,
                RegistrationToken: rotated);
        }

        await RemoveSessionAsync(pending, cancellationToken);

        if (pending.ExistingUserId.HasValue)
        {
            var addRoleResult = await _identityAccountService.AddPlatformRoleAsync(
                pending.ExistingUserId.Value,
                pending.Role,
                cancellationToken);
            if (!addRoleResult.Succeeded || addRoleResult.Account is null)
            {
                return new PendingCompletionResult(
                    PendingCompletionStatus.Failed,
                    Errors: addRoleResult.Errors ?? ["PLATFORM_ROLE_LINK_FAILED"]);
            }

            var linked = addRoleResult.Account;
            if (!linked.EmailConfirmed)
            {
                var confirmResult = await _identityAccountService.ConfirmEmailAsync(linked.Id, cancellationToken);
                if (confirmResult.Succeeded && confirmResult.Account is not null)
                {
                    linked = confirmResult.Account;
                }
            }

            return new PendingCompletionResult(
                PendingCompletionStatus.Succeeded,
                linked,
                pending.Role,
                pending.PayloadJson,
                LinkedExistingAccount: true,
                RegistrationEmail: pending.Email,
                RegistrationPhone: pending.PhoneNumber);
        }

        var createResult = await _identityAccountService.CreateWithPasswordHashAsync(
            new CreateIdentityAccountRequest(
                pending.FullName,
                pending.Email,
                pending.PhoneNumber,
                pending.Role,
                Password: string.Empty,
                pending.ProfilePhotoUrl),
            pending.PasswordHash,
            emailConfirmed: true,
            cancellationToken);

        if (createResult.Status == IdentityCreateStatus.DuplicateEmailOrPhone)
        {
            return new PendingCompletionResult(
                PendingCompletionStatus.Failed,
                Errors: ["USER_ALREADY_EXISTS"]);
        }

        if (createResult.Status != IdentityCreateStatus.Succeeded || createResult.Account is null)
        {
            return new PendingCompletionResult(
                PendingCompletionStatus.Failed,
                Errors: createResult.Errors);
        }

        return new PendingCompletionResult(
            PendingCompletionStatus.Succeeded,
            createResult.Account,
            pending.Role,
            pending.PayloadJson,
            RegistrationEmail: pending.Email,
            RegistrationPhone: pending.PhoneNumber);
    }

    private async Task<PendingRegistration?> ResolveSessionAsync(
        string? registrationToken,
        string? identifier,
        UserRole? expectedRole,
        CancellationToken cancellationToken)
    {
        TryReadSession(registrationToken, out var fromToken);
        if (fromToken is not null && !RoleMatches(fromToken, expectedRole))
        {
            fromToken = null;
        }

        if (fromToken is not null &&
            !string.IsNullOrWhiteSpace(identifier) &&
            !SessionMatchesIdentifier(fromToken, identifier))
        {
            fromToken = null;
        }

        var fromCache = await ReadFromCacheAsync(identifier, expectedRole, cancellationToken);
        if (fromCache is not null && !RoleMatches(fromCache, expectedRole))
        {
            fromCache = null;
        }

        if (fromCache is not null && fromToken is not null)
        {
            return fromCache.UpdatedAtUtc >= fromToken.UpdatedAtUtc ? fromCache : fromToken;
        }

        return fromCache ?? fromToken;
    }

    private async Task<PendingRegistration?> ReadFromCacheAsync(
        string? identifier,
        UserRole? expectedRole,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        var roles = expectedRole.HasValue ? [expectedRole.Value] : LookupRoles;
        foreach (var role in roles)
        {
            foreach (var key in IdentifierKeys(role, identifier))
            {
                var json = await _cache.GetStringAsync(key, cancellationToken);
                var pending = DeserializeSession(json);
                if (pending is not null)
                {
                    return pending;
                }
            }
        }

        return null;
    }

    private async Task PersistSessionAsync(PendingRegistration pending, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(ToSession(pending), JsonOptions);
        var expires = AsUtcDateTime(pending.ExpiresAtUtc);
        var ttl = expires - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = new DateTimeOffset(expires, TimeSpan.Zero)
        };

        foreach (var key in EnumerateCacheKeys(pending))
        {
            await _cache.SetStringAsync(key, json, options, cancellationToken);
        }
    }

    private async Task RemoveSessionAsync(PendingRegistration pending, CancellationToken cancellationToken)
    {
        foreach (var key in EnumerateCacheKeys(pending))
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
    }

    private string CreateToken(PendingRegistration pending)
    {
        var secret = _configuration["JwtSettings:Secret"];
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("JWT Secret is not configured.");
        }

        var sessionJson = JsonSerializer.Serialize(ToSession(pending), JsonOptions);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, pending.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TokenUseClaim, TokenUseValue),
            new(SessionClaim, sessionJson),
            new(ClaimTypes.Email, pending.Email),
            new(ClaimTypes.Role, pending.Role.ToString())
        };

        if (!string.IsNullOrWhiteSpace(pending.PhoneNumber))
        {
            claims.Add(new Claim(ClaimTypes.MobilePhone, pending.PhoneNumber));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = AsUtcDateTime(pending.ExpiresAtUtc);

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private bool TryReadSession(string? registrationToken, out PendingRegistration? pending)
    {
        pending = null;
        if (string.IsNullOrWhiteSpace(registrationToken))
        {
            return false;
        }

        var secret = _configuration["JwtSettings:Secret"];
        if (string.IsNullOrEmpty(secret))
        {
            return false;
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _configuration["JwtSettings:Issuer"],
            ValidateAudience = true,
            ValidAudience = _configuration["JwtSettings:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(registrationToken, parameters, out _);
            if (!string.Equals(
                    principal.FindFirst(TokenUseClaim)?.Value,
                    TokenUseValue,
                    StringComparison.Ordinal))
            {
                return false;
            }

            pending = DeserializeSession(principal.FindFirst(SessionClaim)?.Value);
            return pending is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static PendingRegistration? DeserializeSession(string? sessionJson)
    {
        if (string.IsNullOrWhiteSpace(sessionJson))
        {
            return null;
        }

        try
        {
            var session = JsonSerializer.Deserialize<RegistrationSessionDto>(sessionJson, JsonOptions);
            if (session is null || !Enum.TryParse<UserRole>(session.Role, ignoreCase: true, out var role))
            {
                return null;
            }

            return PendingRegistration.Rehydrate(
                session.Id,
                session.Email,
                session.PhoneNumber,
                session.PasswordHash,
                session.FullName,
                role,
                session.PayloadJson,
                session.ProfilePhotoUrl,
                session.OtpCodeHash,
                session.OtpExpiryUtc,
                session.OtpAttempts,
                session.LastOtpSentAtUtc,
                session.CreatedAtUtc,
                session.UpdatedAtUtc,
                session.ExpiresAtUtc,
                session.ExistingUserId,
                session.LinkedOtpEmail);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string HashPassword(string password)
    {
        var probe = new User("Pending", "pending@zadna.invalid", "0000000000", UserRole.Customer);
        return _userManager.PasswordHasher.HashPassword(probe, password);
    }

    private static RegistrationSessionDto ToSession(PendingRegistration pending) =>
        new(
            pending.Id,
            pending.Email,
            pending.PhoneNumber,
            pending.PasswordHash,
            pending.FullName,
            pending.Role.ToString(),
            pending.PayloadJson,
            pending.ProfilePhotoUrl,
            pending.OtpCodeHash,
            pending.OtpExpiryUtc,
            pending.OtpAttempts,
            pending.LastOtpSentAtUtc,
            pending.CreatedAtUtc,
            pending.UpdatedAtUtc,
            pending.ExpiresAtUtc,
            pending.ExistingUserId,
            pending.LinkedOtpEmail);

    private static PendingRegistrationSnapshot Map(PendingRegistration pending) =>
        new(
            pending.Id,
            pending.FullName,
            pending.Email,
            pending.PhoneNumber,
            pending.Role,
            pending.ProfilePhotoUrl,
            pending.ExistingUserId);

    private Task<Guid?> ResolveLinkableAccountAsync(
        string email,
        string? phone,
        UserRole registeringAs,
        CancellationToken cancellationToken) =>
        PlatformAccountLinkResolver.ResolveAsync(
            _identityAccountService,
            email,
            phone,
            registeringAs,
            cancellationToken);

    private static bool RoleMatches(PendingRegistration pending, UserRole? expectedRole) =>
        !expectedRole.HasValue || pending.Role == expectedRole.Value;

    private static bool SessionMatchesIdentifier(PendingRegistration pending, string identifier) =>
        RegistrationContactMatcher.Matches(identifier, pending.Email, pending.PhoneNumber) ||
        RegistrationContactMatcher.Matches(identifier, pending.LinkedOtpEmail, null);

    private static IEnumerable<string> EnumerateCacheKeys(PendingRegistration pending)
    {
        yield return $"{CachePrefix}id:{pending.Id:N}";
        yield return EmailKey(pending.Role, pending.Email);

        if (!string.IsNullOrWhiteSpace(pending.LinkedOtpEmail))
        {
            yield return EmailKey(pending.Role, pending.LinkedOtpEmail);
        }

        foreach (var phoneKey in PhoneKeys(pending.Role, pending.PhoneNumber))
        {
            yield return phoneKey;
        }
    }

    private static IEnumerable<string> IdentifierKeys(UserRole role, string identifier)
    {
        var trimmed = identifier.Trim();
        if (trimmed.Contains('@', StringComparison.Ordinal))
        {
            yield return EmailKey(role, trimmed);
            yield break;
        }

        foreach (var phoneKey in PhoneKeys(role, trimmed))
        {
            yield return phoneKey;
        }
    }

    private static string EmailKey(UserRole role, string email) =>
        $"{CachePrefix}{role}:email:{email.Trim().ToLowerInvariant()}";

    private static IEnumerable<string> PhoneKeys(UserRole role, string? phone)
    {
        var digits = RegistrationContactMatcher.DigitsOnly(phone);
        if (digits.Length < 8)
        {
            yield break;
        }

        yield return $"{CachePrefix}{role}:phone:{digits}";
        var last9 = digits.Length > 9 ? digits[^9..] : digits;
        yield return $"{CachePrefix}{role}:p9:{last9}";
    }

    private static DateTime AsUtcDateTime(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new UtcDateTimeConverter());
        options.Converters.Add(new UtcNullableDateTimeConverter());
        return options;
    }

    private sealed record RegistrationSessionDto(
        Guid Id,
        string Email,
        string? PhoneNumber,
        string PasswordHash,
        string FullName,
        string Role,
        string PayloadJson,
        string? ProfilePhotoUrl,
        string? OtpCodeHash,
        DateTime? OtpExpiryUtc,
        int OtpAttempts,
        DateTime? LastOtpSentAtUtc,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        DateTime ExpiresAtUtc,
        Guid? ExistingUserId = null,
        string? LinkedOtpEmail = null);

    private sealed class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetDateTime();
            return AsUtcDateTime(value);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(AsUtcDateTime(value).ToString("O", CultureInfo.InvariantCulture));
    }

    private sealed class UtcNullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            return AsUtcDateTime(reader.GetDateTime());
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(AsUtcDateTime(value.Value).ToString("O", CultureInfo.InvariantCulture));
        }
    }
}
