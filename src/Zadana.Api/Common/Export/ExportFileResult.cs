using Microsoft.AspNetCore.Mvc;
using Zadana.Application.Common.Export;

namespace Zadana.Api.Common.Export;

public static class ExportFileResult
{
    public static FileContentResult From(ExportFile file) =>
        new(file.Bytes, file.ContentType)
        {
            FileDownloadName = file.FileName
        };

    public static string StampFileName(string entity, string extension)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
        var safe = string.Join("-", entity.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .Trim('-');
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "export";
        }

        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        return $"{safe}-{stamp}{ext}";
    }
}
