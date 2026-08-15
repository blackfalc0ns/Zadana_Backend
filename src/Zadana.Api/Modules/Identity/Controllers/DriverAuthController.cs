using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Zadana.Api.Security;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/drivers/auth")]
[Tags("Driver App API")]
public class DriverAuthController : IdentityAuthControllerBase
{
    public DriverAuthController(IStringLocalizer<SharedResource> localizer)
        : base(localizer)
    {
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("login")]
    public Task<IActionResult> Login([FromBody] LoginRequest request) =>
        LoginAsync(request, UserRole.Driver);

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [BotChallenge]
    [HttpPost("forgot-password")]
    public Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request) =>
        ForgotPasswordAsync(request, UserRole.Driver);

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("verify-otp")]
    public Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request) =>
        VerifyOtpAsync(request);

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
    public Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request) =>
        RefreshTokenAsync(request, UserRole.Driver);

    [Authorize(Policy = "DriverOnly")]
    [HttpPost("logout")]
    public Task<IActionResult> Logout([FromBody] LogoutRequest request) =>
        LogoutAsync(request);

    [Authorize(Policy = "DriverOnly")]
    [HttpGet("me")]
    public Task<IActionResult> GetCurrentUser() =>
        GetCurrentUserAsync();

    [Authorize(Policy = "DriverOnly")]
    [HttpPut("me")]
    public Task<IActionResult> UpdateCurrentUser([FromBody] UpdateProfileRequest request) =>
        UpdateCurrentUserAsync(request);

    [Authorize(Policy = "DriverOnly")]
    [HttpPut("me/profile-photo")]
    public Task<IActionResult> UpdateCurrentUserProfilePhoto([FromBody] UpdateProfilePhotoRequest request) =>
        UpdateCurrentUserProfilePhotoAsync(request);

    [Authorize(Policy = "DriverOnly")]
    [HttpDelete("me/profile-photo")]
    public Task<IActionResult> DeleteCurrentUserProfilePhoto() =>
        DeleteCurrentUserProfilePhotoAsync();
}
