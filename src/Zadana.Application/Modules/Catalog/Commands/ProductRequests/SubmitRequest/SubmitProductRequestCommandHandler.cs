using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.SubmitRequest;

public class SubmitProductRequestCommandHandler : IRequestHandler<SubmitProductRequestCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentVendorService _currentVendorService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SubmitProductRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentVendorService currentVendorService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentVendorService = currentVendorService;
        _localizer = localizer;
    }

    public async Task<Guid> Handle(SubmitProductRequestCommand request, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException(_localizer["VENDOR_LOGIN_REQUIRED"]);

        var categoryExists = await _context.Categories.FindAsync([request.SuggestedCategoryId], cancellationToken);
        if (categoryExists == null)
        {
            throw new NotFoundException(nameof(Category), request.SuggestedCategoryId);
        }

        var productRequest = new ProductRequest(
            vendorId: vendorId,
            suggestedNameAr: request.SuggestedNameAr,
            suggestedNameEn: request.SuggestedNameEn,
            suggestedCategoryId: request.SuggestedCategoryId,
            suggestedDescriptionAr: request.SuggestedDescriptionAr,
            suggestedDescriptionEn: request.SuggestedDescriptionEn,
            imageUrl: request.ImageUrl
        );

        _context.ProductRequests.Add(productRequest);
        await _context.SaveChangesAsync(cancellationToken);

        return productRequest.Id;
    }
}
