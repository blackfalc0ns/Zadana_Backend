using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Zadana.Api.Security;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.ChangePassword;
using Zadana.Application.Modules.Identity.Commands.ChangeTemporaryPassword;
using Zadana.Application.Modules.Identity.Commands.Login;
using Zadana.Application.Modules.Identity.Commands.Logout;
using Zadana.Application.Modules.Identity.Commands.RefreshToken;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/admin/auth")]
[Tags("Admin Dashboard API")]
public class AdminAuthController : IdentityAuthControllerBase
{
    // Refresh-token cookie lifetime mirrors the server-side refresh-token
    // lifetime configured in <see cref="Zadana.Application.Modules.Identity.Services.RegistrationWorkflow"/>.
    private static readonly TimeSpan RefreshTokenCookieLifetime = TimeSpan.FromDays(7);

    private readonly IWebHostEnvironment _environment;
    private readonly IAntiforgery _antiforgery;

    public AdminAuthController(
        IStringLocalizer<SharedResource> localizer,
        IWebHostEnvironment environment,
        IAntiforgery antiforgery)
        : base(localizer)
    {
        _environment = environment;
        _antiforgery = antiforgery;
    }

    /// <summary>
    /// Issues a fresh anti-CSRF token to the caller. Always succeeds, even when
    /// the user is not yet authenticated; callers must hold this token to be
    /// allowed to perform state-changing admin requests once they log in.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult IssueCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

        // The X-XSRF-TOKEN cookie is what the SPA reads (non-HttpOnly) and
        // mirrors back via the request header for AntiForgery validation.
        Response.Cookies.Append(
            "XSRF-TOKEN",
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = _environment.IsProduction(),
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddHours(2)
            });

        return Ok(new { csrfToken = tokens.RequestToken });
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("login")]
    [ValidateCsrfToken]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await Sender.Send(new LoginCommand(request.Identifier, request.Password, new[] { UserRole.Admin, UserRole.SuperAdmin }));

        WriteRefreshCookie(result.Tokens);
        return Ok(StripRefreshToken(result));
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("refresh-token")]
    [ValidateCsrfToken]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = AdminRefreshCookie.ReadFromRequest(Request, _environment);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new { code = "MISSING_REFRESH_TOKEN" });
        }

        var pair = await Sender.Send(new RefreshTokenCommand(refreshToken));

        AdminRefreshCookie.Write(
            Response,
            _environment,
            pair.RefreshToken,
            DateTimeOffset.UtcNow.Add(RefreshTokenCookieLifetime));

        // Only the access token leaves the server in the response body.
        return Ok(new { accessToken = pair.AccessToken });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("logout")]
    [ValidateCsrfToken]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = AdminRefreshCookie.ReadFromRequest(Request, _environment);

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await Sender.Send(new LogoutCommand(refreshToken));
        }

        AdminRefreshCookie.Clear(Response, _environment);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("change-temporary-password")]
    [ValidateCsrfToken]
    public async Task<IActionResult> ChangeTemporaryPassword([FromBody] ChangeTemporaryPasswordRequest request)
    {
        await Sender.Send(new ChangeTemporaryPasswordCommand(request.CurrentPassword, request.NewPassword));
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("change-password")]
    [ValidateCsrfToken]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        await Sender.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword));
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("me")]
    public Task<IActionResult> GetCurrentUser() =>
        GetCurrentUserAsync();

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("me")]
    [ValidateCsrfToken]
    public Task<IActionResult> UpdateCurrentUser([FromBody] UpdateProfileRequest request) =>
        UpdateCurrentUserAsync(request);

    private void WriteRefreshCookie(TokenPairDto? tokens)
    {
        if (tokens is null || string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return;
        }

        AdminRefreshCookie.Write(
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

        // The refresh token is now delivered via HttpOnly cookie; we keep
        // returning the access token in the body so the SPA can hold it
        // in-memory. Replacing the refresh value with empty signals to legacy
        // clients that they should rely on the cookie.
        var sanitisedPair = new TokenPairDto(source.Tokens.AccessToken, string.Empty);
        return source with { Tokens = sanitisedPair };
    }
}
