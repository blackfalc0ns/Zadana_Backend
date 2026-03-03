using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Zadana.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _sender;
    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
