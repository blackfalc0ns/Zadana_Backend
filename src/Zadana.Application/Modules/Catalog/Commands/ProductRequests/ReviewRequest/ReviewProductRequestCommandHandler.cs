using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.ReviewRequest;

public class ReviewProductRequestCommandHandler : IRequestHandler<ReviewProductRequestCommand, Guid?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ReviewProductRequestCommandHandler(
        IApplicationDbContext context, 
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentUserService = currentUserService;
        _localizer = localizer;
    }

    public async Task<Guid?> Handle(ReviewProductRequestCommand request, CancellationToken cancellationToken)
    {
        // Only Admin or SuperAdmin can review
        if (_currentUserService.Role != "Admin" && _currentUserService.Role != "SuperAdmin")
        {
            throw new UnauthorizedAccessException(_localizer["UNAUTHORIZED_REVIEW_REQUESTS"]);
        }

        var productRequest = await _context.ProductRequests.FindAsync([request.ProductRequestId], cancellationToken);
        if (productRequest == null)
            throw new NotFoundException(nameof(ProductRequest), request.ProductRequestId);

        if (productRequest.Status != Domain.Modules.Catalog.Enums.ApprovalStatus.Pending)
        {
            throw new InvalidOperationException(_localizer["REQUEST_ALREADY_REVIEWED"]);
        }

        if (request.IsApproved)
        {
            productRequest.Approve();

            // Generate slug from English name or Arabic name
            var slug = !string.IsNullOrWhiteSpace(productRequest.SuggestedNameEn) 
                ? productRequest.SuggestedNameEn.ToLowerInvariant().Replace(" ", "-")
                : productRequest.SuggestedNameAr.Replace(" ", "-");

            // Create new MasterProduct
            var masterProduct = new MasterProduct(
                nameAr: productRequest.SuggestedNameAr,
                nameEn: productRequest.SuggestedNameEn,
                slug: slug,
                categoryId: productRequest.SuggestedCategoryId,
                descriptionAr: productRequest.SuggestedDescriptionAr,
                descriptionEn: productRequest.SuggestedDescriptionEn
            );

            _context.MasterProducts.Add(masterProduct);

            // Link image directly using the new unified structure
            if (!string.IsNullOrWhiteSpace(productRequest.ImageUrl))
            {
                masterProduct.AddImage(productRequest.ImageUrl, productRequest.SuggestedNameEn, 0, true);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return masterProduct.Id;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RejectionReason))
                throw new ArgumentException(_localizer["REJECTION_REASON_REQUIRED"]);

            productRequest.Reject(request.RejectionReason);
            await _context.SaveChangesAsync(cancellationToken);
            return null; // No MasterProduct created
        }
    }
}
