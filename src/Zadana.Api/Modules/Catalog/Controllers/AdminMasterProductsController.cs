using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.GetMasterProductById;
using Zadana.Application.Modules.Catalog.Queries.GetMasterProducts;

namespace Zadana.Api.Modules.Catalog.Controllers;

[ApiController]
[Route("api/admin/catalog/products")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminMasterProductsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminMasterProductsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedList<MasterProductDto>>> GetProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? categoryId = null)
    {
        var result = await _sender.Send(new GetMasterProductsQuery(searchTerm, categoryId, pageNumber, pageSize));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MasterProductDto>> GetProduct(Guid id)
    {
        var result = await _sender.Send(new GetMasterProductByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateProduct(CreateMasterProductCommand command)
    {
        var result = await _sender.Send(command);
        return Ok(result);
    }
}
