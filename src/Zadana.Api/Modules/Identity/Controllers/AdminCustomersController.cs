using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Identity.Queries.AdminCustomers;

namespace Zadana.Api.Modules.Identity.Controllers;

[Route("api/admin/customers")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin Dashboard API")]
public class AdminCustomersController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await Sender.Send(new GetAdminCustomersQuery(search, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{customerId:guid}")]
    public async Task<IActionResult> GetCustomerDetail(Guid customerId)
    {
        var result = await Sender.Send(new GetAdminCustomerDetailQuery(customerId));
        return Ok(result);
    }
}
