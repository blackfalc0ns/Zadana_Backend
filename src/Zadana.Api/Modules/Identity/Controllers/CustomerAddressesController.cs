using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.AddCustomerAddress;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/customers/addresses")]
[Tags("🙋‍♂️ 1. Customer App API")]
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

    [HttpPost]
    public async Task<IActionResult> AddAddress([FromBody] AddCustomerAddressRequest request)
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
            request.Longitude
        );

        var result = await Sender.Send(command);
        return Ok(result);
    }
}
