using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Marketing.Requests;
using Zadana.Application.Modules.Marketing.Commands.Coupons;
using Zadana.Application.Modules.Marketing.Commands.CreateCoupon;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Application.Modules.Marketing.Queries.Coupons;

namespace Zadana.Api.Modules.Marketing.Controllers;

[Route("api/admin/marketing/coupons")]
[Authorize(Policy = "AdminOnly")]
[Tags("Marketing (Admins)")]
public class AdminMarketingCouponsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CouponAdminDto>>> GetCoupons()
    {
        var result = await Sender.Send(new GetCouponsQuery());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CouponAdminDto>> GetCoupon(Guid id)
    {
        var result = await Sender.Send(new GetCouponByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CouponAdminDto>> CreateCoupon([FromBody] CreateCouponRequest request)
    {
        var result = await Sender.Send(new CreateCouponCommand(
            request.Code,
            request.Title,
            request.DiscountType,
            request.DiscountValue,
            request.MinOrderAmount,
            request.MaxDiscountAmount,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.UsageLimit,
            request.PerUserLimit,
            request.VendorIds));

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CouponAdminDto>> UpdateCoupon(Guid id, [FromBody] UpdateCouponRequest request)
    {
        var result = await Sender.Send(new UpdateCouponCommand(
            id,
            request.Code,
            request.Title,
            request.DiscountType,
            request.DiscountValue,
            request.MinOrderAmount,
            request.MaxDiscountAmount,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.UsageLimit,
            request.PerUserLimit,
            request.IsActive,
            request.VendorIds));

        return Ok(result);
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<ActionResult> Activate(Guid id)
    {
        await Sender.Send(new ActivateCouponCommand(id));
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<ActionResult> Deactivate(Guid id)
    {
        await Sender.Send(new DeactivateCouponCommand(id));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        await Sender.Send(new DeleteCouponCommand(id));
        return NoContent();
    }
}
