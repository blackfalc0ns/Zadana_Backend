using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Zadana.Api.Security;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.Login;
using Zadana.Application.Modules.Identity.Commands.Logout;
using Zadana.Application.Modules.Identity.Commands.RefreshToken;
using Zadana.Application.Modules.Identity.Commands.VendorGoogleAuth;
using Zadana.Application.Modules.Identity.Commands.VerifyOtp;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/vendors/auth")]
[Tags("Vendor App API")]
public class VendorAuthController : IdentityAuthControllerBase
{
    private static readonly TimeSpan RefreshTokenCookieLifetime = TimeSpan.FromDays(7);

    private readonly IWebHostEnvironment _environment;

    public VendorAuthController(
        IStringLocalizer<SharedResource> localizer,
        IWebHostEnvironment environment)
        : base(localizer)
    {
        _environment = environment;
    }

    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult IssueCsrfToken()
    {
        return Ok(new { csrfToken = ApiCsrfToken.Issue(Response, _environment) });
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("login")]
    [ValidateCsrfToken]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await Sender.Send(new LoginCommand(request.Identifier, request.Password, new[] { UserRole.Vendor, UserRole.VendorStaff }));
        WriteRefreshCookie(result.Tokens);
        return Ok(StripRefreshToken(result));
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("google")]
    [ValidateCsrfToken]
    public async Task<IActionResult> GoogleAuth([FromBody] VendorGoogleAuthRequest request)
    {
        var result = await Sender.Send(new VendorGoogleAuthCommand(request.IdToken));
        if (result.Mode == "login" && result.Auth is not null)
        {
            WriteRefreshCookie(result.Auth.Tokens);
            return Ok(new
            {
                mode = result.Mode,
                auth = StripRefreshToken(result.Auth)
            });
        }

        return Ok(new
        {
            mode = result.Mode,
            profile = result.Profile
        });
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [BotChallenge]
    [HttpPost("forgot-password")]
    public Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request) =>
        ForgotPasswordAsync(request);

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("verify-otp")]
    [ValidateCsrfToken]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var result = await Sender.Send(new VerifyOtpCommand(request.Identifier, request.OtpCode));
        WriteRefreshCookie(result.Tokens);
        return Ok(StripRefreshToken(result));
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("resend-otp")]
    public Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request) =>
        ResendOtpAsync(request);

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("resend-reset-otp")]
    public Task<IActionResult> ResendPasswordResetOtp([FromBody] ResendOtpRequest request) =>
        ResendPasswordResetOtpAsync(request);

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("verify-reset-otp")]
    public Task<IActionResult> VerifyResetOtp([FromBody] VerifyPasswordResetOtpRequest request) =>
        VerifyPasswordResetOtpAsync(request);

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("reset-password")]
    public Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request) =>
        ResetPasswordAsync(request);

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("refresh-token")]
    [ValidateCsrfToken]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? request)
    {
        var refreshToken = VendorRefreshCookie.ReadFromRequest(Request, _environment);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            refreshToken = request?.RefreshToken;
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new
            {
                code = "MISSING_REFRESH_TOKEN",
                message = "Missing refresh token."
            });
        }

        var pair = await Sender.Send(new RefreshTokenCommand(refreshToken));
        VendorRefreshCookie.Write(
            Response,
            _environment,
            pair.RefreshToken,
            DateTimeOffset.UtcNow.Add(RefreshTokenCookieLifetime));

        return Ok(new { accessToken = pair.AccessToken, tokens = new TokenPairDto(pair.AccessToken, string.Empty) });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ValidateCsrfToken]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
    {
        var refreshToken = VendorRefreshCookie.ReadFromRequest(Request, _environment);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            refreshToken = request?.RefreshToken;
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await Sender.Send(new LogoutCommand(refreshToken));
        }

        VendorRefreshCookie.Clear(Response, _environment);
        return NoContent();
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("me")]
    public Task<IActionResult> GetCurrentUser() =>
        GetCurrentUserAsync();

    [Authorize(Policy = "VendorOnly")]
    [HttpPut("me")]
    public Task<IActionResult> UpdateCurrentUser([FromBody] UpdateProfileRequest request) =>
        UpdateCurrentUserAsync(request);

    [Authorize(Policy = "VendorOnly")]
    [HttpPut("me/profile-photo")]
    public Task<IActionResult> UpdateCurrentUserProfilePhoto([FromBody] UpdateProfilePhotoRequest request) =>
        UpdateCurrentUserProfilePhotoAsync(request);

    [Authorize(Policy = "VendorOnly")]
    [HttpDelete("me/profile-photo")]
    public Task<IActionResult> DeleteCurrentUserProfilePhoto() =>
        DeleteCurrentUserProfilePhotoAsync();

    private void WriteRefreshCookie(TokenPairDto? tokens)
    {
        if (tokens is null || string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return;
        }

        VendorRefreshCookie.Write(
            Response,
            _environment,
            tokens.RefreshToken,
            DateTimeOffset.UtcNow.Add(RefreshTokenCookieLifetime));
    }

    private static AuthResponseDto StripRefreshToken(AuthResponseDto source)
    {
        if (source.Tokens is null)
        {
            return source;
        }

        var sanitisedPair = new TokenPairDto(source.Tokens.AccessToken, string.Empty);
        return source with { Tokens = sanitisedPair };
    }
}
