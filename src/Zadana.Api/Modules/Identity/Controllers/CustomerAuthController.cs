using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Zadana.Api.Security;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.RegisterCustomer;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/customers/auth")]
[Tags("Customer App API")]
public class CustomerAuthController : IdentityAuthControllerBase
{
    public CustomerAuthController(IStringLocalizer<SharedResource> localizer)
        : base(localizer)
    {
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [BotChallenge]
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

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("login")]
    public Task<IActionResult> Login([FromBody] LoginRequest request) =>
        LoginAsync(request, UserRole.Customer);

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("refresh-token")]
    public Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request) =>
        RefreshTokenAsync(request, UserRole.Customer);

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
    [BotChallenge]
    [HttpPost("forgot-password")]
    public Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request) =>
        ForgotPasswordAsync(request, UserRole.Customer);

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("verify-reset-otp")]
    public Task<IActionResult> VerifyResetOtp([FromBody] VerifyPasswordResetOtpRequest request) =>
        VerifyPasswordResetOtpAsync(request);

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
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

    [Authorize(Policy = "CustomerOnly")]
    [HttpPut("me")]
    public Task<IActionResult> UpdateCurrentUser([FromBody] UpdateProfileRequest request) =>
        UpdateCurrentUserAsync(request);

    [Authorize(Policy = "CustomerOnly")]
    [HttpPut("me/profile-photo")]
    public Task<IActionResult> UpdateCurrentUserProfilePhoto([FromBody] UpdateProfilePhotoRequest request) =>
        UpdateCurrentUserProfilePhotoAsync(request);

    [Authorize(Policy = "CustomerOnly")]
    [HttpDelete("me/profile-photo")]
    public Task<IActionResult> DeleteCurrentUserProfilePhoto() =>
        DeleteCurrentUserProfilePhotoAsync();

    /// <summary>
    /// Closes (soft-deletes) the customer account. Appears as deleted to the user;
    /// orders and payments remain for history.
    /// </summary>
    [Authorize(Policy = "CustomerOnly")]
    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("close-account")]
    public async Task<IActionResult> CloseAccount(
        [FromBody] CloseAccountRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IAccountClosureService accountClosureService,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BadRequestException("INVALID_REQUEST_BODY", "Request body is required.");
        }

        var userId = currentUserService.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        await accountClosureService.CloseCustomerAccountAsync(
            userId,
            request.Password,
            request.Confirmation,
            request.Reason,
            cancellationToken);

        return Ok(new
        {
            message = "تم حذف الحساب.|Account deleted.",
            closed = true
        });
    }
}
