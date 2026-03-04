using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Modules.Identity.Commands.Login;
using Zadana.Application.Modules.Identity.Commands.ForgotPassword;
using Zadana.Application.Modules.Identity.Commands.ResetPassword;
using Zadana.Application.Modules.Identity.Commands.Logout;
using Zadana.Application.Modules.Identity.Commands.RefreshToken;
using Zadana.Application.Modules.Identity.Queries.GetCurrentUser;
using Zadana.Domain.Modules.Identity.Enums;

using Zadana.Application.Common.Localization;
using Microsoft.Extensions.Localization;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/vendors/auth")]
[Tags("🏪 2. Vendor App API")]
public class VendorAuthController : ApiControllerBase
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    public VendorAuthController(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await Sender.Send(new LoginCommand(request.Identifier, request.Password, [UserRole.Vendor, UserRole.VendorStaff]));
        return Ok(result);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        await Sender.Send(command);
        return Ok(new { Message = _localizer["PasswordResetOtpSent"].Value });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        await Sender.Send(command);
        return Ok(new { Message = _localizer["PasswordResetSuccess"].Value });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand command)
    {
        await Sender.Send(command);
        return NoContent();
    }

    [Authorize(Policy = "VendorOnly")]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var result = await Sender.Send(new GetCurrentUserQuery());
        return Ok(result);
    }
}
