using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Files.Commands.DeleteFile;

public record DeleteFileCommand(Guid FileId) : IRequest;

public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand>
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteFileCommandHandler(
        IFileStorageService fileStorageService,
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _fileStorageService = fileStorageService;
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        var imageBankRecord = await _context.ImageBanks.FindAsync([request.FileId], cancellationToken);
        
        if (imageBankRecord == null)
            throw new NotFoundException(nameof(ImageBank), request.FileId);

        // Security check: Only the owner or an Admin can delete the file
        bool isAdmin = _currentUserService.Role == "SuperAdmin" || _currentUserService.Role == "Admin";
        bool isOwner = imageBankRecord.UploadedByVendorId == _currentUserService.UserId;

        if (!isAdmin && !isOwner)
            throw new UnauthorizedAccessException("غير مصرح لك بحذف هذا الملف | You are not authorized to delete this file");

        // 1. Delete from Cloud Storage
        await _fileStorageService.DeleteAsync(imageBankRecord.Url, cancellationToken);

        // 2. Delete from Database
        _context.ImageBanks.Remove(imageBankRecord);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
