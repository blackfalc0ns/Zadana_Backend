using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;

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
}
