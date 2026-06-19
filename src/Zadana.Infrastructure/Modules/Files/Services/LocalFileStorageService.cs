using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Modules.Files.Services;

/// <summary>
/// Stores uploads in a persistent media directory outside the API publish
/// folder. Common raster images are normalized to WebP; PDFs remain unchanged.
/// A separate IIS/static site should map the configured public origin to the
/// same physical root.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private static readonly HashSet<string> RasterImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
        };

    private readonly LocalMediaStorageSettings _settings;
    private readonly string _rootPath;
    private readonly Uri _publicBaseUri;
    private readonly SemaphoreSlim _imageProcessingGate;

    public LocalFileStorageService(
        IOptions<FileStorageSettings> options,
        IHostEnvironment environment)
    {
        _settings = options.Value.Local;

        if (string.IsNullOrWhiteSpace(_settings.RootPath))
        {
            throw new InvalidOperationException("FileStorage:Local:RootPath is required for local media storage.");
        }

        if (!Uri.TryCreate(_settings.PublicBaseUrl?.TrimEnd('/') + "/", UriKind.Absolute, out var publicBaseUri) ||
            (publicBaseUri.Scheme != Uri.UriSchemeHttps && publicBaseUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("FileStorage:Local:PublicBaseUrl must be an absolute HTTP(S) URL.");
        }

        _publicBaseUri = publicBaseUri;
        _imageProcessingGate = new SemaphoreSlim(
            _settings.MaxConcurrentImageProcessors,
            _settings.MaxConcurrentImageProcessors);

        var configuredRoot = Environment.ExpandEnvironmentVariables(_settings.RootPath);
        _rootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.Combine(environment.ContentRootPath, configuredRoot));

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> UploadAsync(
        FileUploadDto file,
        string directory,
        CancellationToken cancellationToken = default)
    {
        ValidateReadableStream(file.ContentStream);

        var safeDirectory = NormalizeDirectory(directory);
        var physicalDirectory = ResolveUnderRoot(safeDirectory);
        Directory.CreateDirectory(physicalDirectory);

        var sourceExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var convertToWebp = _settings.ConvertImagesToWebp &&
                            RasterImageExtensions.Contains(sourceExtension);
        var outputExtension = convertToWebp ? ".webp" : sourceExtension;

        if (string.IsNullOrWhiteSpace(outputExtension))
        {
            throw new BadRequestException("INVALID_FILE_EXTENSION", "The uploaded file must have a valid extension.");
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{outputExtension}";
        var finalPath = Path.Combine(physicalDirectory, uniqueFileName);
        var temporaryPath = finalPath + ".uploading";

        try
        {
            if (convertToWebp)
            {
                await _imageProcessingGate.WaitAsync(cancellationToken);
                try
                {
                    await ConvertToWebpAsync(file.ContentStream, temporaryPath, cancellationToken);
                }
                finally
                {
                    _imageProcessingGate.Release();
                }
            }
            else
            {
                ResetStream(file.ContentStream);
                await using var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    useAsync: true);
                await file.ContentStream.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, finalPath);
        }
        catch
        {
            TryDeleteFile(temporaryPath);
            throw;
        }

        var relativeUrl = string.IsNullOrEmpty(safeDirectory)
            ? uniqueFileName
            : $"{safeDirectory}/{uniqueFileName}";
        return new Uri(_publicBaseUri, relativeUrl).AbsoluteUri;
    }

    public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl) ||
            !Uri.TryCreate(fileUrl, UriKind.Absolute, out var fileUri) ||
            !HasSameOrigin(_publicBaseUri, fileUri))
        {
            return Task.CompletedTask;
        }

        var basePath = Uri.UnescapeDataString(_publicBaseUri.AbsolutePath).Trim('/');
        var filePath = Uri.UnescapeDataString(fileUri.AbsolutePath).Trim('/');

        if (!string.IsNullOrEmpty(basePath))
        {
            if (!filePath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            filePath = filePath[(basePath.Length + 1)..];
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.CompletedTask;
        }

        var fullPath = ResolveUnderRoot(filePath);
        TryDeleteFile(fullPath);
        return Task.CompletedTask;
    }

    private async Task ConvertToWebpAsync(
        Stream source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        ResetStream(source);

        SKImageInfo info;
        using (var encodedInput = new SKManagedStream(source, disposeManagedStream: false))
        using (var codec = SKCodec.Create(encodedInput)
               ?? throw new BadRequestException("INVALID_IMAGE", "The uploaded file is not a decodable image."))
        {
            info = codec.Info;
        }

        if (info.Width <= 0 || info.Height <= 0 ||
            (long)info.Width * info.Height > _settings.MaxPixelCount)
        {
            throw new BadRequestException(
                "IMAGE_DIMENSIONS_TOO_LARGE",
                "The uploaded image dimensions exceed the allowed limit.");
        }

        ResetStream(source);
        using var original = SKBitmap.Decode(source)
            ?? throw new BadRequestException("INVALID_IMAGE", "The uploaded file is not a decodable image.");

        var scale = Math.Min(
            1d,
            Math.Min(
                _settings.MaxWidth / (double)original.Width,
                _settings.MaxHeight / (double)original.Height));
        var targetWidth = Math.Max(1, (int)Math.Round(original.Width * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(original.Height * scale));

        using var resized = scale < 1d
            ? original.Resize(
                new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul),
                SKSamplingOptions.Default)
            : null;
        var bitmapToEncode = resized ?? original;

        using var image = SKImage.FromBitmap(bitmapToEncode);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, _settings.WebpQuality)
            ?? throw new InvalidOperationException("Failed to encode the uploaded image as WebP.");

        await using var output = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);
        encoded.SaveTo(output);
        await output.FlushAsync(cancellationToken);
    }

    private string ResolveUnderRoot(string relativePath)
    {
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalizedRelativePath));
        var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The media path resolves outside the configured storage root.");
        }

        return fullPath;
    }

    private static string NormalizeDirectory(string directory)
    {
        var normalized = (directory ?? string.Empty)
            .Replace('\\', '/')
            .Trim('/');

        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(normalized))
        {
            throw new BadRequestException("INVALID_UPLOAD_DIRECTORY", "The upload directory is invalid.");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".." ||
                                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new BadRequestException("INVALID_UPLOAD_DIRECTORY", "The upload directory is invalid.");
        }

        return string.Join('/', segments);
    }

    private static void ValidateReadableStream(Stream stream)
    {
        if (stream is null || !stream.CanRead)
        {
            throw new ArgumentException("File stream cannot be empty.");
        }

        if (stream.CanSeek && stream.Length == 0)
        {
            throw new ArgumentException("File stream cannot be empty.");
        }
    }

    private static void ResetStream(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
    }

    private static bool HasSameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
        left.Port == right.Port;

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Cleanup is best-effort; a later maintenance job may retry it.
        }
        catch (UnauthorizedAccessException)
        {
            // Do not turn a successful business operation into an error solely
            // because an obsolete media file could not be removed.
        }
    }
}
