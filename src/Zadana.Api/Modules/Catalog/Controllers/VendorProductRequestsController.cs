using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Modules.Catalog.Commands.ProductRequests.SubmitRequest;
using Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetVendorRequests;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/vendor/product-requests")]
[Route("api/vendor/catalog/product-requests")]
[Tags("Catalog (Vendors)")]
[Authorize(Roles = "Vendor")]
public class VendorProductRequestsController : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SubmitRequest([FromBody] SubmitProductRequestRequest request)
    {
        var command = new SubmitProductRequestCommand(
            request.Product.NameAr,
            request.Product.NameEn,
            request.Product.CategoryId,
            request.Product.BrandId,
            request.Product.UnitId,
            request.Product.DescriptionAr,
            request.Product.DescriptionEn,
            request.Product.ImageUrl,
            request.RequestedBrand is null
                ? null
                : new RequestedBrandDraft(
                    request.RequestedBrand.NameAr,
                    request.RequestedBrand.NameEn,
                    request.RequestedBrand.LogoUrl),
            request.RequestedCategory is null
                ? null
                : new RequestedCategoryDraft(
                    request.RequestedCategory.NameAr,
                    request.RequestedCategory.NameEn,
                    request.RequestedCategory.ParentCategoryId,
                    request.RequestedCategory.DisplayOrder,
                    request.RequestedCategory.ImageUrl));

        var requestId = await Sender.Send(command);
        return Ok(new { ProductRequestId = requestId });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyRequests([FromQuery] GetVendorProductRequestsRequest request)
    {
        var query = new GetVendorProductRequestsQuery(request.PageNumber, request.PageSize, request.Status);
        var result = await Sender.Send(query);
        return Ok(result);
    }
}

