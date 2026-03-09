using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Files.Commands.DeleteFile;

public record DeleteFileCommand(Guid FileId) : IRequest;

public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand>
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public DeleteFileCommandHandler(
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

    public async Task Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        // DeleteFile functionality is temporarily disabled due to ImageBank removal.
        // It should be refactored to use direct storage deletion if needed, 
        // It should be refactored to use direct storage deletion if needed, 
        // but current UI workflows were tied to ImageBank records.
        throw new NotImplementedException(_localizer["FEATURE_UNDER_REFACTORING"]);
    }
}
