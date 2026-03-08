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
        // DeleteFile functionality is temporarily disabled due to ImageBank removal.
        // It should be refactored to use direct storage deletion if needed, 
        // but current UI workflows were tied to ImageBank records.
        throw new NotImplementedException("DeleteFile is being refactored. | جاري إعادة بناء وظيفة مسح الملفات.");
    }
}
