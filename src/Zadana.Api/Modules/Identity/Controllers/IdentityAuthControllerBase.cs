using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.ForgotPassword;
using Zadana.Application.Modules.Identity.Commands.Login;
using Zadana.Application.Modules.Identity.Commands.Logout;
using Zadana.Application.Modules.Identity.Commands.RefreshToken;
using Zadana.Application.Modules.Identity.Commands.ResetPassword;
using Zadana.Application.Modules.Identity.Commands.UpdateCurrentUserProfile;
using Zadana.Application.Modules.Identity.Queries.GetCurrentUser;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Api.Modules.Identity.Controllers;

public abstract class IdentityAuthControllerBase : ApiControllerBase
{
    private readonly IStringLocalizer<SharedResource> _localizer;

    protected IdentityAuthControllerBase(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    protected async Task<IActionResult> LoginAsync(LoginRequest request, params UserRole[] roles)
    {
        var result = await Sender.Send(new LoginCommand(request.Identifier, request.Password, roles));
        return Ok(result);
    }

    protected async Task<IActionResult> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var result = await Sender.Send(new RefreshTokenCommand(request.RefreshToken));
        return Ok(result);
    }

    protected async Task<IActionResult> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        await Sender.Send(new ForgotPasswordCommand(request.Identifier));
        return Ok(new { Message = _localizer["PasswordResetOtpSent"].Value });
    }

    protected async Task<IActionResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        await Sender.Send(new ResetPasswordCommand(request.Identifier, request.OtpCode, request.NewPassword));
        return Ok(new { Message = _localizer["PasswordResetSuccess"].Value });
    }

    protected async Task<IActionResult> LogoutAsync(LogoutRequest request)
    {
        await Sender.Send(new LogoutCommand(request.RefreshToken));
        return NoContent();
    }

    protected async Task<IActionResult> GetCurrentUserAsync()
    {
        var result = await Sender.Send(new GetCurrentUserQuery());
        return Ok(result);
    }

    protected async Task<IActionResult> UpdateCurrentUserAsync(UpdateProfileRequest request)
    {
        var result = await Sender.Send(new UpdateCurrentUserProfileCommand(
            request.FullName,
            request.Email,
            request.Phone));

        return Ok(result);
    }
}
