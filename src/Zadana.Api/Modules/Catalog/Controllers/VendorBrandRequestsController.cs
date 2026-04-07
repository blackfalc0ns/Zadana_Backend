using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Modules.Catalog.Commands.BrandRequests.SubmitRequest;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/vendor/catalog/brand-requests")]
[Tags("Catalog Vendors")]
[Authorize(Roles = "Vendor")]
public class VendorBrandRequestsController : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SubmitRequest([FromBody] SubmitBrandRequestPayload request)
    {
        var requestId = await Sender.Send(new SubmitBrandRequestCommand(
            request.NameAr,
            request.NameEn,
            request.LogoUrl));

        return Ok(new { BrandRequestId = requestId });
    }
}
