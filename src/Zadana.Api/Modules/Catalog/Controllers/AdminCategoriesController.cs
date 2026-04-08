using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Catalog.Requests;
using Zadana.Application.Modules.Catalog.Commands.Categories.CreateCategory;
using Zadana.Application.Modules.Catalog.Commands.Categories.DeleteCategory;
using Zadana.Application.Modules.Catalog.Commands.Categories.UpdateCategory;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategories;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryById;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/catalog/categories")]
[Authorize(Roles = "Admin,SuperAdmin")]
[Tags("Catalog (Admins)")]
public class AdminCategoriesController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories([FromQuery] bool includeInactive = false)
    {
        var result = await Sender.Send(new GetCategoriesQuery(includeInactive));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        var command = new CreateCategoryCommand(
            request.NameAr,
            request.NameEn,
            request.ImageUrl,
            request.ParentCategoryId,
            request.DisplayOrder);

        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        var command = new UpdateCategoryCommand(
            id,
            request.NameAr,
            request.NameEn,
            request.ImageUrl,
            request.ParentCategoryId,
            request.DisplayOrder,
            request.IsActive);

        await Sender.Send(command);
        return Ok();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDto>> GetCategoryById(Guid id)
    {
        var result = await Sender.Send(new GetCategoryByIdQuery(id));
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteCategory(Guid id)
    {
        await Sender.Send(new DeleteCategoryCommand(id));
        return NoContent();
    }
}

