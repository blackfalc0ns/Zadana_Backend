using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Support;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Infrastructure.Modules.Identity.Services;

/// <summary>
/// Holds pending signup state in a short-lived signed JWT. No DB row is created until OTP succeeds.
/// </summary>
public sealed class PendingRegistrationService : IPendingRegistrationService
{
    public const string TokenUseClaim = "token_use";
    public const string TokenUseValue = "registration";
    public const string SessionClaim = "reg_session";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IIdentityAccountService _identityAccountService;
    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _configuration;

    public PendingRegistrationService(
        IIdentityAccountService identityAccountService,
        UserManager<User> userManager,
        IConfiguration configuration)
    {
        _identityAccountService = identityAccountService;
        _userManager = userManager;
        _configuration = configuration;
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

        return new PendingRegistrationStartResult(
            PendingRegistrationStartStatus.Succeeded,
            Map(pending),
            otp,
            token);
    }

    public Task<PendingOtpDispatchResult> ResendOtpAsync(
        string registrationToken,
        UserRole? expectedRole = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadSession(registrationToken, out var pending) || pending is null)
        {
            return Task.FromResult(new PendingOtpDispatchResult(PendingOtpDispatchStatus.NotFound));
        }

        if (expectedRole.HasValue && pending.Role != expectedRole.Value)
        {
            return Task.FromResult(new PendingOtpDispatchResult(PendingOtpDispatchStatus.NotFound));
        }

        if (pending.IsExpired())
        {
            return Task.FromResult(new PendingOtpDispatchResult(PendingOtpDispatchStatus.Expired));
        }

        if (!pending.CanResendOtp())
        {
            return Task.FromResult(new PendingOtpDispatchResult(
                PendingOtpDispatchStatus.CooldownActive,
                Map(pending),
                RegistrationToken: registrationToken,
                CooldownSecondsRemaining: pending.ResendCooldownSecondsRemaining()));
        }

        var otp = pending.GenerateOtp();
        var token = CreateToken(pending);
        return Task.FromResult(new PendingOtpDispatchResult(
            PendingOtpDispatchStatus.Succeeded,
            Map(pending),
            otp,
            token));
    }

    public async Task<PendingCompletionResult> VerifyAndCreateAccountAsync(
        string registrationToken,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadSession(registrationToken, out var pending) || pending is null)
        {
            return new PendingCompletionResult(PendingCompletionStatus.NotFound);
        }

        if (pending.IsExpired())
        {
            return new PendingCompletionResult(PendingCompletionStatus.Expired);
        }

        if (!pending.VerifyOtp(otpCode))
        {
            // Return a rotated token so OTP attempt counters remain enforceable without DB state.
            var rotated = CreateToken(pending);
            return new PendingCompletionResult(
                PendingCompletionStatus.InvalidOtp,
                RegistrationToken: rotated);
        }

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
        var expires = pending.ExpiresAtUtc;

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

            var sessionJson = principal.FindFirst(SessionClaim)?.Value;
            if (string.IsNullOrWhiteSpace(sessionJson))
            {
                return false;
            }

            var session = JsonSerializer.Deserialize<RegistrationSessionDto>(sessionJson, JsonOptions);
            if (session is null || !Enum.TryParse<UserRole>(session.Role, ignoreCase: true, out var role))
            {
                return false;
            }

            pending = PendingRegistration.Rehydrate(
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
            return true;
        }
        catch (SecurityTokenException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
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
            pending.ExistingUserId,
            pending.LinkedOtpEmail);

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
}
