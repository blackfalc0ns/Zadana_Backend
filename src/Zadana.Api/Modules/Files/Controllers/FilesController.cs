using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Files.Security;
using Zadana.Api.Security;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Files.Commands.UploadFile;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Files.Controllers;

[Route("api/files")]
[Tags("Common Systems (Files)")]
public class FilesController(ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpPost("upload")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.FileUploads)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string directory)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is empty.");
        }

        if (!FileUploadSecurityPolicy.TryResolve(directory, out var rule))
        {
            throw new BadRequestException("INVALID_UPLOAD_DIRECTORY", "The upload directory is not allowed.");
        }

        if (file.Length > rule.MaxFileSizeBytes)
        {
            throw new BadRequestException("FILE_TOO_LARGE", $"File size exceeds the allowed limit of {rule.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        if (!rule.AllowAnonymous && !currentUserService.IsAuthenticated)
        {
            return Challenge();
        }

        if (currentUserService.IsAuthenticated &&
            rule.AllowedRoles.Count > 0 &&
            (string.IsNullOrWhiteSpace(currentUserService.Role) || !rule.AllowedRoles.Contains(currentUserService.Role)))
        {
            return Forbid();
        }

        using var stream = file.OpenReadStream();
        var fileDto = new FileUploadDto(Path.GetFileName(file.FileName), file.ContentType, stream);
        var command = new UploadFileCommand(rule.Directory, fileDto);
        var fileUrl = await Sender.Send(command);
        return Ok(new { url = fileUrl });
    }
}
