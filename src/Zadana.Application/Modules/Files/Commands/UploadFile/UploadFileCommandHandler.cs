using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Files.Commands.UploadFile;

public class UploadFileCommandHandler : IRequestHandler<UploadFileCommand, string>
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const long MaxPdfBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, FileValidationRule> AllowedFileTypes =
        new Dictionary<string, FileValidationRule>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new(["image/jpeg", "application/octet-stream"], MaxImageBytes, [[0xFF, 0xD8, 0xFF]]),
            [".jpeg"] = new(["image/jpeg", "application/octet-stream"], MaxImageBytes, [[0xFF, 0xD8, 0xFF]]),
            [".png"] = new(["image/png", "application/octet-stream"], MaxImageBytes, [[0x89, 0x50, 0x4E, 0x47]]),
            [".webp"] = new(["image/webp", "application/octet-stream"], MaxImageBytes, [[0x52, 0x49, 0x46, 0x46], [0x57, 0x45, 0x42, 0x50]]),
            [".pdf"] = new(["application/pdf", "application/octet-stream"], MaxPdfBytes, [[0x25, 0x50, 0x44, 0x46]])
        };

    private readonly IFileStorageService _fileStorageService;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public UploadFileCommandHandler(
        IFileStorageService fileStorageService,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer)
    {
        _fileStorageService = fileStorageService;
        _context = context;
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    public async Task<string> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.ContentStream == null || !request.File.ContentStream.CanRead)
        {
            throw new BadRequestException("NO_FILE_PROVIDED", _localizer["NO_FILE_PROVIDED"]);
        }

        if (request.File.ContentStream.CanSeek)
        {
            request.File.ContentStream.Position = 0;

            if (request.File.ContentStream.Length == 0)
            {
                throw new BadRequestException("NO_FILE_PROVIDED", _localizer["NO_FILE_PROVIDED"]);
            }
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!AllowedFileTypes.TryGetValue(extension, out var validationRule))
        {
            throw new BadRequestException(
                "INVALID_FILE_EXTENSION",
                _localizer["INVALID_FILE_EXTENSION", string.Join(", ", AllowedFileTypes.Keys)]);
        }

        if (!string.IsNullOrWhiteSpace(request.File.ContentType) &&
            !validationRule.AllowedContentTypes.Contains(request.File.ContentType.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new BadRequestException("INVALID_FILE_CONTENT_TYPE", "The uploaded file content type is not allowed.");
        }

        if (request.File.ContentStream.CanSeek && request.File.ContentStream.Length > validationRule.MaxBytes)
        {
            throw new BadRequestException("FILE_TOO_LARGE", $"The uploaded file exceeds the allowed size limit of {validationRule.MaxBytes / (1024 * 1024)} MB.");
        }

        await EnsureFileSignatureAsync(request.File, validationRule, cancellationToken);

        var sanitizedFile = request.File with { FileName = Path.GetFileName(request.File.FileName) };
        var fileUrl = await _fileStorageService.UploadAsync(sanitizedFile, request.Directory, cancellationToken);
        return fileUrl;
    }

    private static async Task EnsureFileSignatureAsync(FileUploadDto file, FileValidationRule rule, CancellationToken cancellationToken)
    {
        if (!file.ContentStream.CanSeek)
        {
            return;
        }

        file.ContentStream.Position = 0;
        var maxSignatureLength = file.FileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            ? 12
            : rule.Signatures.Max(signature => signature.Length);
        var buffer = new byte[maxSignatureLength];
        var bytesRead = await file.ContentStream.ReadAsync(buffer.AsMemory(0, maxSignatureLength), cancellationToken);
        file.ContentStream.Position = 0;

        var matchesSignature = file.FileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            ? bytesRead >= 12 &&
              buffer.AsSpan(0, 4).SequenceEqual(rule.Signatures[0]) &&
              buffer.AsSpan(8, 4).SequenceEqual(rule.Signatures[1])
            : rule.Signatures.Any(signature =>
                bytesRead >= signature.Length &&
                buffer.AsSpan(0, signature.Length).SequenceEqual(signature));

        if (!matchesSignature)
        {
            throw new BadRequestException("INVALID_FILE_SIGNATURE", "The uploaded file content does not match the declared file type.");
        }
    }

    private sealed record FileValidationRule(
        string[] AllowedContentTypes,
        long MaxBytes,
        byte[][] Signatures);
}
