using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.AddCustomerAddress;
using Zadana.Application.Modules.Identity.Commands.DeleteCustomerAddress;
using Zadana.Application.Modules.Identity.Commands.SetDefaultCustomerAddress;
using Zadana.Application.Modules.Identity.Commands.UpdateCustomerAddress;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Queries.GetCustomerAddresses;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/customers/addresses")]
[Tags("Customer App API")]
[Authorize(Policy = "CustomerOnly")]
public class CustomerAddressesController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CustomerAddressesController(
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer)
    {
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerAddressDto>>> GetAddresses()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);

        var result = await Sender.Send(new GetCustomerAddressesQuery(userId.Value));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerAddressDto>> AddAddress([FromBody] AddCustomerAddressRequest request)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);

        var command = new AddCustomerAddressCommand(
            userId.Value,
            request.ContactName,
            request.ContactPhone,
            request.AddressLine,
            request.Label,
            request.BuildingNo,
            request.FloorNo,
            request.ApartmentNo,
            request.City,
            request.Area,
            request.Latitude,
            request.Longitude,
            request.IsDefault
        );

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPut("{addressId:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid addressId, [FromBody] AddCustomerAddressRequest request)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);

        var command = new UpdateCustomerAddressCommand(
            addressId,
            userId.Value,
            request.ContactName,
            request.ContactPhone,
            request.AddressLine,
            request.Label,
            request.BuildingNo,
            request.FloorNo,
            request.ApartmentNo,
            request.City,
            request.Area,
            request.Latitude,
            request.Longitude,
            request.IsDefault
        );

        await Sender.Send(command);
        return NoContent();
    }

    [HttpPatch("{addressId:guid}/default")]
    public async Task<IActionResult> SetDefaultAddress(Guid addressId)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);

        await Sender.Send(new SetDefaultCustomerAddressCommand(addressId, userId.Value));
        return NoContent();
    }

    [HttpDelete("{addressId:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid addressId)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            throw new UnauthorizedException(_localizer["UserNotAuthenticated"]);

        await Sender.Send(new DeleteCustomerAddressCommand(addressId, userId.Value));
        return NoContent();
    }
}

