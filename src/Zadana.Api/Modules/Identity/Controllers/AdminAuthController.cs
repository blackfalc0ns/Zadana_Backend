using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Localization;
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

    [HttpPost("login")]
    public Task<IActionResult> Login([FromBody] LoginRequest request) =>
        LoginAsync(request, UserRole.Admin, UserRole.SuperAdmin);

    [HttpPost("refresh-token")]
    public Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request) =>
        RefreshTokenAsync(request);

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("logout")]
    public Task<IActionResult> Logout([FromBody] LogoutRequest request) =>
        LogoutAsync(request);

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("me")]
    public Task<IActionResult> GetCurrentUser() =>
        GetCurrentUserAsync();
}
