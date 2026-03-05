using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Application.Modules.Catalog.Commands.Brands.CreateBrand;
using Zadana.Application.Modules.Catalog.Commands.Brands.UpdateBrand;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrands;

namespace Zadana.Api.Modules.Catalog.Controllers;

[ApiController]
[Route("api/admin/catalog/brands")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminBrandsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminBrandsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<List<BrandDto>>> GetBrands([FromQuery] bool includeInactive = false)
    {
        var result = await _sender.Send(new GetBrandsQuery(includeInactive));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<BrandDto>> CreateBrand(CreateBrandCommand command)
    {
        var result = await _sender.Send(command);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateBrand(Guid id, [FromBody] UpdateBrandRequest request)
    {
        var command = new UpdateBrandCommand(
            id,
            request.NameAr,
            request.NameEn,
            request.LogoUrl,
            request.IsActive);

        await _sender.Send(command);
        return Ok();
    }
}

public record UpdateBrandRequest(
    string NameAr,
    string NameEn,
    string? LogoUrl,
    bool IsActive);
