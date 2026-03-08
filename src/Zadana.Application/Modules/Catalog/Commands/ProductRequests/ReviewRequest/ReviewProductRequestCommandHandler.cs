using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.ReviewRequest;

public class ReviewProductRequestCommandHandler : IRequestHandler<ReviewProductRequestCommand, Guid?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ReviewProductRequestCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Guid?> Handle(ReviewProductRequestCommand request, CancellationToken cancellationToken)
    {
        // Only Admin or SuperAdmin can review
        if (_currentUserService.Role != "Admin" && _currentUserService.Role != "SuperAdmin")
        {
            throw new UnauthorizedAccessException("غير مصرح لك بمراجعة طلبات المنتجات | You are not authorized to review product requests.");
        }

        var productRequest = await _context.ProductRequests.FindAsync([request.ProductRequestId], cancellationToken);
        if (productRequest == null)
            throw new NotFoundException(nameof(ProductRequest), request.ProductRequestId);

        if (productRequest.Status != Domain.Modules.Catalog.Enums.ApprovalStatus.Pending)
        {
            throw new InvalidOperationException("هذا الطلب تمت مراجعته مسبقاً | This request has already been reviewed.");
        }

        if (request.IsApproved)
        {
            productRequest.Approve();

            // Create new MasterProduct
            var masterProduct = new MasterProduct(
                nameAr: productRequest.SuggestedNameAr,
                nameEn: productRequest.SuggestedNameEn,
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
                throw new ArgumentException("سبب الرفض مطلوب | Rejection reason is required.");

            productRequest.Reject(request.RejectionReason);
            await _context.SaveChangesAsync(cancellationToken);
            return null; // No MasterProduct created
        }
    }
}
