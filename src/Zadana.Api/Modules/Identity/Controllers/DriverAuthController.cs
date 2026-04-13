using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
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

    [HttpPost("login")]
    public Task<IActionResult> Login([FromBody] LoginRequest request) =>
        LoginAsync(request, UserRole.Driver);

    [HttpPost("forgot-password")]
    public Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request) =>
        ForgotPasswordAsync(request);

    [HttpPost("reset-password")]
    public Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request) =>
        ResetPasswordAsync(request);

    [HttpPost("refresh-token")]
    public Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request) =>
        RefreshTokenAsync(request);

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
}
