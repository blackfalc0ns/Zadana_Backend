using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Modules.Identity.Commands.Login;
using Zadana.Application.Modules.Identity.Commands.Logout;
using Zadana.Application.Modules.Identity.Commands.RefreshToken;
using Zadana.Application.Modules.Identity.Commands.RegisterCustomer;
using Zadana.Application.Modules.Identity.Queries.GetCurrentUser;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/customers/auth")]
[Tags("📱 Customer App - Auth")]
public class CustomerAuthController : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await Sender.Send(new LoginCommand(request.Identifier, request.Password, [UserRole.Customer]));
        return Ok(result);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }

    [Authorize(Policy = "CustomerOnly")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand command)
    {
        await Sender.Send(command);
        return NoContent();
    }

    [Authorize(Policy = "CustomerOnly")]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var result = await Sender.Send(new GetCurrentUserQuery());
        return Ok(result);
    }
}
