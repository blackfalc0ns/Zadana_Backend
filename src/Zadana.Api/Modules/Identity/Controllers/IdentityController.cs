using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Identity.Commands.Login;
using Zadana.Application.Modules.Identity.Commands.Logout;
using Zadana.Application.Modules.Identity.Commands.RefreshToken;
using Zadana.Application.Modules.Identity.Commands.RegisterCustomer;
using Zadana.Application.Modules.Identity.Queries.GetCurrentUser;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Api.Modules.Identity.Controllers;

public record LoginRequest(string Identifier, string Password);

[Route("api/identity")]
[Tags("🔐 Auth - Identity")]
public class IdentityController : ApiControllerBase
{
    /// <summary>
    /// تسجيل مستخدم عادي (Customer)
    /// </summary>
    [HttpPost("register-customer")]
    public async Task<IActionResult> RegisterCustomer([FromBody] RegisterCustomerCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// تسجيل دخول تطبيق المستخدم المستهلك
    /// </summary>
    [HttpPost("login-customer")]
    public async Task<IActionResult> LoginCustomer([FromBody] LoginRequest request)
    {
        var result = await Sender.Send(new LoginCommand(request.Identifier, request.Password, [UserRole.Customer]));
        return Ok(result);
    }

    /// <summary>
    /// تسجيل دخول تطبيق المندوب
    /// </summary>
    [HttpPost("login-driver")]
    public async Task<IActionResult> LoginDriver([FromBody] LoginRequest request)
    {
        var result = await Sender.Send(new LoginCommand(request.Identifier, request.Password, [UserRole.Driver]));
        return Ok(result);
    }

    /// <summary>
    /// تسجيل دخول لوحة تحكم التاجر
    /// </summary>
    [HttpPost("login-vendor")]
    public async Task<IActionResult> LoginVendor([FromBody] LoginRequest request)
    {
        var result = await Sender.Send(new LoginCommand(request.Identifier, request.Password, [UserRole.Vendor, UserRole.VendorStaff]));
        return Ok(result);
    }

    /// <summary>
    /// تسجيل دخول لوحة تحكم الإدارة (المنصة)
    /// </summary>
    [HttpPost("login-admin")]
    public async Task<IActionResult> LoginAdmin([FromBody] LoginRequest request)
    {
        var result = await Sender.Send(new LoginCommand(request.Identifier, request.Password, [UserRole.Admin, UserRole.SuperAdmin]));
        return Ok(result);
    }

    /// <summary>
    /// تحديث الـ Token
    /// </summary>
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// تسجيل الخروج
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand command)
    {
        await Sender.Send(command);
        return NoContent();
    }

    /// <summary>
    /// بيانات المستخدم الحالي
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var result = await Sender.Send(new GetCurrentUserQuery());
        return Ok(result);
    }
}
