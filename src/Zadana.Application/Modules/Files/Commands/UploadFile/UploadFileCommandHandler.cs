using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Files.Commands.UploadFile;

public class UploadFileCommandHandler : IRequestHandler<UploadFileCommand, string>
{
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
        if (request.File == null || request.File.ContentStream == null || request.File.ContentStream.Length == 0)
        {
            throw new BadRequestException("NO_FILE_PROVIDED", _localizer["NO_FILE_PROVIDED"]);
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };

        if (!allowedExtensions.Contains(extension))
        {
            throw new BadRequestException(
                "INVALID_FILE_EXTENSION",
                _localizer["INVALID_FILE_EXTENSION", string.Join(", ", allowedExtensions)]);
        }

        var fileUrl = await _fileStorageService.UploadAsync(request.File, request.Directory, cancellationToken);
        return fileUrl;
    }
}
