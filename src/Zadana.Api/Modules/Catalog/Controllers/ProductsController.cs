using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Products.GetProductDetails;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/products")]
[AllowAnonymous]
[Tags("Customer App API")]
public class ProductsController : ApiControllerBase
{
    [HttpGet("{productId:guid}")]
    public async Task<ActionResult<ProductDetailsDto>> GetProduct(Guid productId, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetProductDetailsQuery(productId), cancellationToken);
        return Ok(result);
    }
}
