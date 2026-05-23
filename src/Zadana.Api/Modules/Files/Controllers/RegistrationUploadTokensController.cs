using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zadana.Api.Controllers;
using Zadana.Api.Security;

namespace Zadana.Api.Modules.Files.Controllers;

/// <summary>
/// Issues short-lived HMAC tokens for unauthenticated registration uploads
/// (driver national-id, vendor commercial register, etc.). These tokens
/// replace the legacy "anyone can POST to /api/files/upload" behavior so
/// random callers cannot dump files into the storage account.
/// </summary>
[Route("api/registration-upload-tokens")]
[Tags("Common Systems (Files)")]
public class RegistrationUploadTokensController(RegistrationUploadTokenService tokenService) : ApiControllerBase
{
    [HttpPost("issue")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    public ActionResult<RegistrationUploadTokenResponse> Issue([FromBody] RegistrationUploadTokenRequest? request)
    {
        var deviceId = request?.DeviceId
            ?? Request.Headers["X-Device-Id"].ToString();
        var issued = tokenService.Issue(
            RegistrationUploadTokenService.PurposeRegistration,
            deviceId ?? string.Empty);

        return Ok(new RegistrationUploadTokenResponse(
            issued.Token,
            issued.ExpiresAtUtc,
            RegistrationUploadTokenService.HeaderName));
    }
}

public sealed record RegistrationUploadTokenRequest(string? DeviceId);

public sealed record RegistrationUploadTokenResponse(
    string Token,
    DateTimeOffset ExpiresAtUtc,
    string HeaderName);
