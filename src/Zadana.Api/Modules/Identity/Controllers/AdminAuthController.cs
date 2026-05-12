using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zadana.Api.Security;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.ChangeTemporaryPassword;
using Zadana.Domain.Modules.Identity.Enums;
using Microsoft.Extensions.Localization;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/admin/auth")]
[Tags("Admin Dashboard API")]
public class AdminAuthController : IdentityAuthControllerBase
{
    public AdminAuthController(IStringLocalizer<SharedResource> localizer)
        : base(localizer)
    {
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("login")]
    public Task<IActionResult> Login([FromBody] LoginRequest request) =>
        LoginAsync(request, UserRole.Admin, UserRole.SuperAdmin);

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("refresh-token")]
    public Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request) =>
        RefreshTokenAsync(request);

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("logout")]
    public Task<IActionResult> Logout([FromBody] LogoutRequest request) =>
        LogoutAsync(request);

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("change-temporary-password")]
    public async Task<IActionResult> ChangeTemporaryPassword([FromBody] ChangeTemporaryPasswordRequest request)
    {
        await Sender.Send(new ChangeTemporaryPasswordCommand(request.CurrentPassword, request.NewPassword));
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("me")]
    public Task<IActionResult> GetCurrentUser() =>
        GetCurrentUserAsync();

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("me")]
    public Task<IActionResult> UpdateCurrentUser([FromBody] UpdateProfileRequest request) =>
        UpdateCurrentUserAsync(request);
}
