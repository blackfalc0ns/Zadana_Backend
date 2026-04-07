using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.CategoryRequests.SubmitRequest;

public class SubmitCategoryRequestCommandHandler : IRequestHandler<SubmitCategoryRequestCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentVendorService _currentVendorService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SubmitCategoryRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentVendorService currentVendorService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentVendorService = currentVendorService;
        _localizer = localizer;
    }

    public async Task<Guid> Handle(SubmitCategoryRequestCommand request, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(cancellationToken)
            ?? throw new ForbiddenAccessException(_localizer["VENDOR_LOGIN_REQUIRED"]);

        if (request.ParentCategoryId.HasValue
            && !await _context.Categories.AnyAsync(category => category.Id == request.ParentCategoryId.Value, cancellationToken))
        {
            throw new NotFoundException(nameof(Category), request.ParentCategoryId.Value);
        }

        var categoryRequest = new CategoryRequest(
            vendorId,
            request.NameAr,
            request.NameEn,
            request.ParentCategoryId,
            request.DisplayOrder,
            request.ImageUrl);

        _context.CategoryRequests.Add(categoryRequest);
        await _context.SaveChangesAsync(cancellationToken);
        return categoryRequest.Id;
    }
}
