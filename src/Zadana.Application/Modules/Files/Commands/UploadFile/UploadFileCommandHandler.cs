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

        // Record it in ImageBank if requested by an authenticated user
        if (_currentUserService.UserId.HasValue)
        {
            // Only set UploadedByVendorId if the role is Vendor
            Guid? vendorId = _currentUserService.Role == "Vendor" ? _currentUserService.UserId : null;
            
            var imageBankEntry = new ImageBank(
                url: fileUrl,
                altText: request.File.FileName,
                tags: request.Directory, // Defaulting tags to directory for simple search filtering
                uploadedByVendorId: vendorId
            );

            // Per User Requirement: If uploaded by a vendor, default Status is Pending.
            // Admin will then approve it. (This behavior is handled automatically in the ImageBank constructor)
            _context.ImageBanks.Add(imageBankEntry);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return fileUrl;
    }
}
