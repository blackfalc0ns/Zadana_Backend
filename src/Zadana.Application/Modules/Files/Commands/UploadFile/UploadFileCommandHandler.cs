using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Files.Commands.UploadFile;

public class UploadFileCommandHandler : IRequestHandler<UploadFileCommand, string>
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UploadFileCommandHandler(
        IFileStorageService fileStorageService,
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _fileStorageService = fileStorageService;
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<string> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.ContentStream == null || request.File.ContentStream.Length == 0)
        {
            throw new ArgumentException("لم يتم توفير ملف. | No file was provided.");
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };

        if (!allowedExtensions.Contains(extension))
        {
            throw new ArgumentException($"امتداد الملف غير صالح. الامتدادات المسموحة هي: | Invalid file extension. Allowed extensions are: {string.Join(", ", allowedExtensions)}");
        }

        // Upload and get public URL
        var fileUrl = await _fileStorageService.UploadAsync(request.File, request.Directory, cancellationToken);

        // Record in database is no longer needed here as ImageBank is removed.
        // The calling component (e.g. MasterProduct creation) will link this URL to its own entities.

        return fileUrl;
    }
}
