using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Catalog.Commands.ProductRequests.ReviewRequest;
using Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetPendingRequests;

namespace Zadana.Api.Modules.Catalog.Controllers;

[Route("api/admin/product-requests")]
[Tags("📦 2. Catalog (Admins)")]
[Authorize]
public class AdminProductRequestsController : ApiControllerBase
{
    /// <summary>
    /// استرجاع كل طلبات المنتجات المعلقة (للأدمن)
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingRequests([FromQuery] GetPendingProductRequestsQuery query)
    {
        var result = await Sender.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// الموافقة أو الرفض على طلب إضافة منتج (للأدمن)
    /// </summary>
    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> ReviewRequest(Guid id, [FromBody] ReviewProductRequestDto dto)
    {
        var command = new ReviewProductRequestCommand(id, dto.IsApproved, dto.RejectionReason);
        var masterProductId = await Sender.Send(command);
        
        return Ok(new { 
            Message = dto.IsApproved ? "تمت الموافقة وتم إنشاء المنتج الأساسي بنجاح." : "تم رفض الطلب.",
            MasterProductId = masterProductId 
        });
    }
}

public class ReviewProductRequestDto
{
    public bool IsApproved { get; set; }
    public string? RejectionReason { get; set; }
}
