using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Common;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.CategoryRequests.SubmitRequest;

public class SubmitCategoryRequestCommandHandler : IRequestHandler<SubmitCategoryRequestCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentVendorService _currentVendorService;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IAdminAlertService _adminAlertService;

    public SubmitCategoryRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentVendorService currentVendorService,
        IStringLocalizer<SharedResource> localizer,
        IAdminAlertService adminAlertService)
    {
        _context = context;
        _currentVendorService = currentVendorService;
        _localizer = localizer;
        _adminAlertService = adminAlertService;
    }

    public async Task<Guid> Handle(SubmitCategoryRequestCommand request, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(cancellationToken)
            ?? throw new ForbiddenAccessException(_localizer["VENDOR_LOGIN_REQUIRED"]);

        var targetLevelKey = await CatalogRequestWorkflowSupport.ValidateAndResolveCategoryTargetLevelAsync(
            _context,
            vendorId,
            request.NameAr,
            request.NameEn,
            request.TargetLevel,
            request.ParentCategoryId,
            cancellationToken);

        var categoryRequest = new CategoryRequest(
            vendorId,
            request.NameAr,
            request.NameEn,
            targetLevelKey,
            request.ParentCategoryId,
            request.DisplayOrder,
            request.ImageUrl);

        _context.CategoryRequests.Add(categoryRequest);
        await _context.SaveChangesAsync(cancellationToken);
        await _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.CatalogCategoryRequestSubmitted,
                AdminAlertCategories.Catalog,
                AdminAlertPriorities.Normal,
                "طلب تصنيف من تاجر",
                "New vendor category request",
                $"تم إرسال طلب تصنيف: {request.NameAr}.",
                $"A new category request was submitted: {request.NameEn}.",
                categoryRequest.Id,
                "/catalog/requests",
                new { categoryRequestId = categoryRequest.Id, vendorId }),
            cancellationToken);
        return categoryRequest.Id;
    }

}
