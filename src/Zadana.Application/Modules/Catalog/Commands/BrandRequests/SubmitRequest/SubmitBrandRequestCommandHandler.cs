using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Microsoft.Extensions.Localization;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.BrandRequests.SubmitRequest;

public class SubmitBrandRequestCommandHandler : IRequestHandler<SubmitBrandRequestCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentVendorService _currentVendorService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SubmitBrandRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentVendorService currentVendorService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentVendorService = currentVendorService;
        _localizer = localizer;
    }

    public async Task<Guid> Handle(SubmitBrandRequestCommand request, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(cancellationToken)
            ?? throw new ForbiddenAccessException(_localizer["VENDOR_LOGIN_REQUIRED"]);

        var category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), request.CategoryId);

        if (category.ParentCategoryId is null)
        {
            throw new BusinessRuleException("BRAND_CATEGORY_MUST_BE_NESTED", _localizer["CATEGORY_PARENT_REQUIRED"]);
        }

        var brandRequest = new BrandRequest(vendorId, request.CategoryId, request.NameAr, request.NameEn, request.LogoUrl);
        _context.BrandRequests.Add(brandRequest);
        await _context.SaveChangesAsync(cancellationToken);
        return brandRequest.Id;
    }
}
