using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Catalog.Queries.ImageBank.GetGallery;
using Zadana.Application.Modules.Files.Commands.DeleteFile;
using Zadana.Application.Modules.Files.Commands.UploadFile;

namespace Zadana.Api.Modules.Files.Controllers;

[Route("api/files")]
[Tags("📁 5. Common Systems (Files)")]
public class FilesController : ApiControllerBase
{
    /// <summary>
    /// رفع ملف (صورة، مستند، سجل تجاري)
    /// </summary>
    [HttpPost("upload")]
    [AllowAnonymous]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string directory = "uploads/general")
    {
        if (file == null || file.Length == 0) return BadRequest("File is empty.");

        using var stream = file.OpenReadStream();
        var fileDto = new Zadana.Application.Common.Interfaces.FileUploadDto(file.FileName, file.ContentType, stream);

        var safeDirectory = directory.Replace("..", "").Trim('/');
        var command = new UploadFileCommand(safeDirectory, fileDto);
        
        var fileUrl = await Sender.Send(command);
        
        return Ok(new { Url = fileUrl });
    }

    /// <summary>
    /// استرجاع معرض الصور الخاص بالمستخدم الحالي (للتجار)
    /// </summary>
    [HttpGet("gallery")]
    [Authorize]
    public async Task<IActionResult> GetGallery([FromQuery] GetImageGalleryQuery query)
    {
        var result = await Sender.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// حذف ملف من السيرفر وقاعدة البيانات
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteFile(Guid id)
    {
        await Sender.Send(new DeleteFileCommand(id));
        return NoContent();
    }
}
