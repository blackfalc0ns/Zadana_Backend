using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Infrastructure.Modules.Files.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _webRootPath;

    public LocalFileStorageService(IWebHostEnvironment environment, IHttpContextAccessor httpContextAccessor)
    {
        _environment = environment;
        _httpContextAccessor = httpContextAccessor;
        _webRootPath = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
    }

    public async Task<string> UploadAsync(FileUploadDto file, string directory, CancellationToken cancellationToken = default)
    {
        if (file.ContentStream == null || !file.ContentStream.CanRead)
        {
            throw new ArgumentException("File stream cannot be empty.");
        }

        if (file.ContentStream.CanSeek)
        {
            file.ContentStream.Position = 0;

            if (file.ContentStream.Length == 0)
            {
                throw new ArgumentException("File stream cannot be empty.");
            }
        }

        // Generate safe unique filename
        var extension = Path.GetExtension(file.FileName);
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";

        // Ensure directory exists in wwwroot
        Directory.CreateDirectory(_webRootPath);

        var uploadPath = Path.Combine(_webRootPath, directory);
        if (!Directory.Exists(uploadPath))
        {
            Directory.CreateDirectory(uploadPath);
        }

        var fullPath = Path.Combine(uploadPath, uniqueFileName);

        using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            await file.ContentStream.CopyToAsync(fileStream, cancellationToken);
        }

        // Return the public URL
        var request = _httpContextAccessor.HttpContext?.Request;
        var baseUrl = $"{request?.Scheme}://{request?.Host}";
        
        // e.g., http://localhost:5000/uploads/vendors/logo.jpg
        return $"{baseUrl}/{directory}/{uniqueFileName}".Replace("\\", "/");
    }

    public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fileUrl)) return Task.CompletedTask;

        try
        {
            var uri = new Uri(fileUrl);
            var localPath = uri.LocalPath.TrimStart('/');
            var fullPath = Path.Combine(_webRootPath, localPath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (UriFormatException)
        {
            // Ignored if the fileUrl is not a valid URI (e.g., seeding data)
        }

        return Task.CompletedTask;
    }
}
