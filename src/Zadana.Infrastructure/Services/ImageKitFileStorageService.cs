using Imagekit.Models;
using Imagekit.Models.Response;
using Imagekit.Sdk;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Services;

public class ImageKitFileStorageService : IFileStorageService
{
    private readonly ImagekitClient _client;

    public ImageKitFileStorageService(IOptions<ImageKitSettings> settings)
    {
        _client = new ImagekitClient(
            settings.Value.PublicKey,
            settings.Value.PrivateKey,
            settings.Value.UrlEndpoint
        );
    }

    public async Task<string> UploadAsync(FileUploadDto file, string directory, CancellationToken cancellationToken = default)
    {
        try
        {
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await file.ContentStream.CopyToAsync(ms, cancellationToken);
                fileBytes = ms.ToArray();
            }

            var request = new FileCreateRequest
            {
                file = fileBytes,
                fileName = file.FileName,
                folder = directory,
                useUniqueFileName = true
            };

            var response = await _client.UploadAsync(request);

            if (response.HttpStatusCode != 200)
            {
                var errorMessage = response.Raw ?? "Unknown error";
                throw new ExternalServiceException(
                    "IMAGEKIT_UPLOAD_FAILED",
                    $"ImageKit upload failed. Status: {response.HttpStatusCode}. Error: {errorMessage}");
            }

            return response.url;
        }
        catch (ExternalServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExternalServiceException("IMAGEKIT_UPLOAD_ERROR", "ImageKit upload failed unexpectedly.", ex);
        }
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        // Extract the fileId from the URL or query it.
        // Imagekit's v5 .NET SDK generally works by using fileId to delete.
        // We first need to get the file search to find ID by URL, or assume we parsed the fileId.
        
        // This is a naive implementation that attempts to fetch the file details filtering by url 
        // to retrieve the fileId needed for deletion.
        string[] urlParts = fileUrl.Split('/');
        string fileName = urlParts[^1];
        
        GetFileListRequest request = new GetFileListRequest
        {
            SearchQuery = $"name=\"{fileName}\""
        };

        var files = await _client.GetFileListRequestAsync(request);

        if (files != null && files.FileList != null && files.FileList.Count > 0)
        {
            var file = files.FileList.FirstOrDefault(f => f.url == fileUrl || f.name == fileName);
            if (file != null)
            {
                await _client.DeleteFileAsync(file.fileId);
            }
        }
    }
}
