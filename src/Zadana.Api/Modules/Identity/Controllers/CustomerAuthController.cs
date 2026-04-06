using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.RegisterCustomer;
using Zadana.Application.Modules.Identity.Commands.ResendOtp;
using Zadana.Application.Modules.Identity.Commands.VerifyOtp;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/customers/auth")]
[Tags("Customer App API")]
public class CustomerAuthController : IdentityAuthControllerBase
{
    public CustomerAuthController(IStringLocalizer<SharedResource> localizer)
        : base(localizer)
    {
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerRequest request)
    {
        var command = new RegisterCustomerCommand(
            request.FullName,
            request.Email,
            request.Phone,
            request.Password,
            request.ProfilePhotoUrl,
            request.AddressLine,
            request.Label,
            request.BuildingNo,
            request.FloorNo,
            request.ApartmentNo,
            request.City,
            request.Area,
            request.Latitude,
            request.Longitude);

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var result = await Sender.Send(new VerifyOtpCommand(request.Identifier, request.OtpCode));
        return Ok(result);
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
    {
        var result = await Sender.Send(new ResendOtpCommand(request.Identifier));
        return Ok(result);
    }

    [HttpPost("login")]
    public Task<IActionResult> Login([FromBody] LoginRequest request) =>
        LoginAsync(request, UserRole.Customer);

    [HttpPost("refresh-token")]
    public Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request) =>
        RefreshTokenAsync(request);

    [HttpPost("forgot-password")]
    public Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request) =>
        ForgotPasswordAsync(request);

    [HttpPost("reset-password")]
    public Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request) =>
        ResetPasswordAsync(request);

    [Authorize(Policy = "CustomerOnly")]
    [HttpPost("logout")]
    public Task<IActionResult> Logout([FromBody] LogoutRequest request) =>
        LogoutAsync(request);

    [Authorize(Policy = "CustomerOnly")]
    [HttpGet("me")]
    public Task<IActionResult> GetCurrentUser() =>
        GetCurrentUserAsync();
}
