using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Application.Modules.Catalog.Commands.Categories.CreateCategory;
using Zadana.Application.Modules.Catalog.Commands.Categories.UpdateCategory;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategories;

namespace Zadana.Api.Modules.Catalog.Controllers;

[ApiController]
[Route("api/admin/catalog/categories")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminCategoriesController : ControllerBase
{
    private readonly ISender _sender;

    public AdminCategoriesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories([FromQuery] bool includeInactive = false)
    {
        var result = await _sender.Send(new GetCategoriesQuery(includeInactive));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory(CreateCategoryCommand command)
    {
        var result = await _sender.Send(command);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        var command = new UpdateCategoryCommand(
            id,
            request.NameAr,
            request.NameEn,
            request.ParentCategoryId,
            request.DisplayOrder,
            request.IsActive);

        await _sender.Send(command);
        return Ok();
    }
}

public record UpdateCategoryRequest(
    string NameAr,
    string NameEn,
    Guid? ParentCategoryId,
    int DisplayOrder,
    bool IsActive);
