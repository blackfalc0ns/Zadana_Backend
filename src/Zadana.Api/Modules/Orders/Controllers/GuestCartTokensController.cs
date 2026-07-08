using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zadana.Api.Controllers;
using Zadana.Api.Localization;
using Zadana.Api.Security;

namespace Zadana.Api.Modules.Orders.Controllers;

/// <summary>
/// Issues an HMAC signature for a guest device id so the mobile app can
/// authenticate every subsequent cart call against tampering.
///
/// The mobile flow:
///   1. POST /api/cart/guest-token  with {"deviceId": "<uuid>"} - receive
///      a signature string.
///   2. Send X-Device-Id and X-Device-Signature on every cart request.
/// </summary>
[Route("api/cart")]
[Tags("Customer App API")]
public sealed class GuestCartTokensController(GuestCartSigner signer) : ApiControllerBase
{
    [HttpPost("guest-token")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    public ActionResult<GuestCartTokenResponse> Issue([FromBody] GuestCartTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return BadRequest(new { code = "DEVICE_ID_REQUIRED", message = ApiLocalizedMessages.Resolve(HttpContext, "DEVICE_ID_REQUIRED") });
        }

        var signature = signer.Sign(request.DeviceId.Trim());
        return Ok(new GuestCartTokenResponse(
            request.DeviceId.Trim(),
            signature,
            GuestCartSigner.DeviceHeader,
            GuestCartSigner.SignatureHeader));
    }
}

public sealed record GuestCartTokenRequest(string DeviceId);

public sealed record GuestCartTokenResponse(
    string DeviceId,
    string Signature,
    string DeviceHeaderName,
    string SignatureHeaderName);
