using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Marketing.Commands.ProductCardPriceVisibility;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Application.Modules.Marketing.Queries.ProductCardPriceVisibility;

namespace Zadana.Api.Modules.Marketing.Controllers;

[Route("api/admin/marketing/product-card-price-visibility")]
[Authorize(Policy = "AdminOnly")]
[Tags("Marketing (Admins)")]
public class AdminMarketingProductCardPriceVisibilityController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProductCardPriceVisibilitySettingDto>> GetSetting(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetProductCardPriceVisibilityQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPatch]
    public async Task<ActionResult<ProductCardPriceVisibilitySettingDto>> SetSetting(
        [FromBody] SetProductCardPriceVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SetAllProductCardPriceVisibilityCommand(request.ShowPriceOnCard),
            cancellationToken);

        return Ok(result);
    }
}

public record SetProductCardPriceVisibilityRequest(bool ShowPriceOnCard);
