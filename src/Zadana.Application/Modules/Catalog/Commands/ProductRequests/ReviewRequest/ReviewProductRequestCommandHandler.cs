using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Extensions;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.ReviewRequest;

public class ReviewProductRequestCommandHandler : IRequestHandler<ReviewProductRequestCommand, Guid?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ReviewProductRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityAccountService identityAccountService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityAccountService = identityAccountService;
        _localizer = localizer;
    }

    public async Task<Guid?> Handle(ReviewProductRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.HasRole(UserRole.Admin, UserRole.SuperAdmin))
        {
            throw new ForbiddenAccessException(_localizer["UNAUTHORIZED_REVIEW_REQUESTS"]);
        }

        var productRequest = await _context.ProductRequests
            .Include(item => item.Vendor)
            .Include(item => item.BrandRequest)
            .Include(item => item.CategoryRequest)
            .FirstOrDefaultAsync(item => item.Id == request.ProductRequestId, cancellationToken);
        if (productRequest == null)
        {
            throw new NotFoundException(nameof(ProductRequest), request.ProductRequestId);
        }

        if (productRequest.Status != ApprovalStatus.Pending)
        {
            throw new BusinessRuleException("REQUEST_ALREADY_REVIEWED", _localizer["REQUEST_ALREADY_REVIEWED"]);
        }

        var reviewerName = await ResolveReviewerNameAsync(cancellationToken);

        if (request.IsApproved)
        {
            var resolvedCategoryId = productRequest.SuggestedCategoryId;
            if (!resolvedCategoryId.HasValue && productRequest.CategoryRequest is not null)
            {
                if (productRequest.CategoryRequest.Status != ApprovalStatus.Approved || !productRequest.CategoryRequest.CreatedCategoryId.HasValue)
                {
                    throw new BusinessRuleException("CATEGORY_REQUEST_NOT_APPROVED", _localizer["REQUEST_ALREADY_REVIEWED"]);
                }

                resolvedCategoryId = productRequest.CategoryRequest.CreatedCategoryId.Value;
            }

            if (!resolvedCategoryId.HasValue)
            {
                throw new BadRequestException("CATEGORY_REQUIRED", _localizer["RequiredField"]);
            }

            var resolvedBrandId = productRequest.SuggestedBrandId;
            if (!resolvedBrandId.HasValue && productRequest.BrandRequest is not null)
            {
                if (productRequest.BrandRequest.Status != ApprovalStatus.Approved || !productRequest.BrandRequest.CreatedBrandId.HasValue)
                {
                    throw new BusinessRuleException("BRAND_REQUEST_NOT_APPROVED", _localizer["REQUEST_ALREADY_REVIEWED"]);
                }

                resolvedBrandId = productRequest.BrandRequest.CreatedBrandId.Value;
            }

            var slug = !string.IsNullOrWhiteSpace(productRequest.SuggestedNameEn)
                ? productRequest.SuggestedNameEn.ToLowerInvariant().Replace(" ", "-")
                : productRequest.SuggestedNameAr.Replace(" ", "-");

            var masterProduct = new MasterProduct(
                nameAr: productRequest.SuggestedNameAr,
                nameEn: productRequest.SuggestedNameEn,
                slug: slug,
                categoryId: resolvedCategoryId.Value,
                brandId: resolvedBrandId,
                unitOfMeasureId: productRequest.SuggestedUnitOfMeasureId,
                descriptionAr: productRequest.SuggestedDescriptionAr,
                descriptionEn: productRequest.SuggestedDescriptionEn
            );

            if (!string.IsNullOrWhiteSpace(productRequest.ImageUrl))
            {
                masterProduct.AddImage(productRequest.ImageUrl, productRequest.SuggestedNameEn, 0, true);
            }

            _context.MasterProducts.Add(masterProduct);
            productRequest.Approve(reviewerName, masterProduct.Id);

            _context.Notifications.Add(new Notification(
                productRequest.Vendor.UserId,
                "Catalog Request Approved",
                $"Your product request '{productRequest.SuggestedNameEn}' has been approved.",
                "catalog_request_product"));

            await _context.SaveChangesAsync(cancellationToken);
            return masterProduct.Id;
        }

        if (string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            throw new BadRequestException("REJECTION_REASON_REQUIRED", _localizer["REJECTION_REASON_REQUIRED"]);
        }

        productRequest.Reject(request.RejectionReason, reviewerName);

        _context.Notifications.Add(new Notification(
            productRequest.Vendor.UserId,
            "Catalog Request Rejected",
            $"Your product request '{productRequest.SuggestedNameEn}' was rejected. Reason: {request.RejectionReason}",
            "catalog_request_product"));

        await _context.SaveChangesAsync(cancellationToken);
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
