using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;
using Zadana.Application.Modules.Orders.Commands.DriverUpdateOrderStatus;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/drivers")]
[Tags("Driver App API")]
public class DriversController : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> RegisterDriver([FromBody] RegisterDriverRequest request)
    {
        var command = new RegisterDriverCommand(
            request.FullName,
            request.Email,
            request.Phone,
            request.Password,
            request.VehicleType,
            request.NationalId,
            request.LicenseNumber,
            request.Address,
            request.NationalIdImageUrl,
            request.LicenseImageUrl,
            request.VehicleImageUrl,
            request.PersonalPhotoUrl);

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPost("orders/{orderId:guid}/picked-up")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOrderStatusResponse>> MarkPickedUp(
        Guid orderId,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new DriverUpdateOrderStatusCommand(orderId, userId, OrderStatus.PickedUp, "Driver picked up the order"),
            cancellationToken);
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.Message));
    }

    [HttpPost("orders/{orderId:guid}/on-the-way")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOrderStatusResponse>> MarkOnTheWay(
        Guid orderId,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new DriverUpdateOrderStatusCommand(orderId, userId, OrderStatus.OnTheWay, "Driver is on the way"),
            cancellationToken);
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.Message));
    }

    [HttpPost("orders/{orderId:guid}/delivered")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOrderStatusResponse>> MarkDelivered(
        Guid orderId,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new DriverUpdateOrderStatusCommand(orderId, userId, OrderStatus.Delivered, "Order delivered successfully"),
            cancellationToken);
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.Message));
    }

    [HttpPost("orders/{orderId:guid}/delivery-failed")]
    [Authorize(Policy = "DriverOnly")]
    public async Task<ActionResult<DriverOrderStatusResponse>> MarkDeliveryFailed(
        Guid orderId,
        [FromBody] DriverDeliveryFailedRequest? request,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var result = await Sender.Send(
            new DriverUpdateOrderStatusCommand(orderId, userId, OrderStatus.DeliveryFailed, request?.Note ?? "Delivery failed"),
            cancellationToken);
        return Ok(new DriverOrderStatusResponse(result.OrderId, result.Status, result.Message));
    }
}

public record DriverOrderStatusResponse(Guid OrderId, string Status, string Message);
public record DriverDeliveryFailedRequest(string? Note);

