using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Extensions;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Common;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.BrandRequests.ReviewRequest;

public class ReviewBrandRequestCommandHandler : IRequestHandler<ReviewBrandRequestCommand, Guid?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly INotificationService _notificationService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<ReviewBrandRequestCommandHandler> _logger;

    public ReviewBrandRequestCommandHandler(
        IApplicationDbContext context,
        ICacheInvalidator cacheInvalidator,
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        IStringLocalizer<SharedResource> localizer,
        INotificationService notificationService,
        IFileStorageService fileStorageService,
        ILogger<ReviewBrandRequestCommandHandler> logger)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _localizer = localizer;
        _notificationService = notificationService;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<Guid?> Handle(ReviewBrandRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.HasRole(UserRole.Admin, UserRole.SuperAdmin))
        {
            throw new ForbiddenAccessException("UNAUTHORIZED_REVIEW_REQUESTS");
        }

        var brandRequest = await _context.BrandRequests
            .Include(x => x.Vendor)
            .FirstOrDefaultAsync(x => x.Id == request.BrandRequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(BrandRequest), request.BrandRequestId);

        if (brandRequest.Status != ApprovalStatus.Pending)
        {
            throw new BusinessRuleException("REQUEST_ALREADY_REVIEWED", _localizer["REQUEST_ALREADY_REVIEWED"]);
        }

        var reviewerName = await ResolveReviewerNameAsync(cancellationToken);

        if (request.IsApproved)
        {
            var categoryExists = await _context.Categories
                .AsNoTracking()
                .AnyAsync(item => item.Id == brandRequest.CategoryId, cancellationToken);
            if (!categoryExists)
            {
                throw new NotFoundException(nameof(Category), brandRequest.CategoryId);
            }

            var brand = await CatalogRequestWorkflowSupport.FindMatchingBrandAsync(
                _context,
                brandRequest.NameAr,
                brandRequest.NameEn,
                cancellationToken);

            if (brand is null)
            {
                brand = new Brand(brandRequest.NameAr, brandRequest.NameEn, brandRequest.LogoUrl, null, brandRequest.CategoryId);
                _context.Brands.Add(brand);
            }
            else
            {
                if (!brand.IsActive)
                {
                    brand.Activate();
                }

                if (string.IsNullOrWhiteSpace(brand.LogoUrl) && !string.IsNullOrWhiteSpace(brandRequest.LogoUrl))
                {
                    brand.Update(brand.NameAr, brand.NameEn, brandRequest.LogoUrl, brand.CoverImageUrl, brand.CategoryId);
                }
            }

            if (!brand.BrandCategories.Any(link => link.CategoryId == brandRequest.CategoryId))
            {
                _context.BrandCategories.Add(new BrandCategory(brand.Id, brandRequest.CategoryId));
            }

            brandRequest.Approve(reviewerName, brand.Id);
            await _notificationService.SendToUserAsync(
                brandRequest.Vendor.UserId,
                "اعتمدنا طلب الكتالوج",
                "Catalog Request Approved",
                $"اعتمدنا طلب العلامة التجارية '{brandRequest.NameAr}'.",
                $"Your brand request '{brandRequest.NameEn}' has been approved.",
                "catalog_request_brand",
                cancellationToken: cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
            return brand.Id;
        }

        if (string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            throw new BadRequestException("REJECTION_REASON_REQUIRED", _localizer["REJECTION_REASON_REQUIRED"]);
        }

        brandRequest.Reject(request.RejectionReason, reviewerName);
        await _notificationService.SendToUserAsync(
            brandRequest.Vendor.UserId,
            "رفضنا طلب الكتالوج",
            "Catalog Request Rejected",
            $"رفضنا طلب العلامة التجارية '{brandRequest.NameAr}'. السبب: {request.RejectionReason}",
            $"Your brand request '{brandRequest.NameEn}' was rejected. Reason: {request.RejectionReason}",
            "catalog_request_brand",
            cancellationToken: cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(brandRequest.LogoUrl))
        {
            try
            {
                await _fileStorageService.DeleteAsync(brandRequest.LogoUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete logo file {LogoUrl} for rejected brand request {BrandRequestId}",
                    brandRequest.LogoUrl, brandRequest.Id);
            }
        }

        return null;
    }

    private async Task<string> ResolveReviewerNameAsync(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return "Admin";
        }

        var reviewer = await _identityAccountService.FindByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        return string.IsNullOrWhiteSpace(reviewer?.FullName) ? "Admin" : reviewer.FullName;
    }
}
