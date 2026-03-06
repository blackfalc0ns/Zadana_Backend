using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Catalog.Commands.ProductRequests.SubmitRequest;
using Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetVendorRequests;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/vendor/product-requests")]
[Tags("📦 2. Catalog (Vendors)")]
[Authorize]
public class VendorProductRequestsController : ApiControllerBase
{
    /// <summary>
    /// تقديم طلب إضافة منتج جديد (للتجار)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitRequest([FromBody] SubmitProductRequestCommand command)
    {
        var requestId = await Sender.Send(command);
        return Ok(new { ProductRequestId = requestId });
    }

    /// <summary>
    /// استرجاع كل طلبات المنتجات للتاجر الحالي
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyRequests([FromQuery] GetVendorProductRequestsQuery query)
    {
        var result = await Sender.Send(query);
        return Ok(result);
    }
}
