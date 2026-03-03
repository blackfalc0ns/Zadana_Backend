namespace Zadana.Application.Common.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    /// Uploads a file and returns the public URL to access it.
    /// </summary>
    Task<string> UploadAsync(FileUploadDto file, string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file given its URL.
    /// </summary>
    Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);
}
