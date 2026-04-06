using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Extensions;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.ProductRequests.ReviewRequest;

public class ReviewProductRequestCommandHandler : IRequestHandler<ReviewProductRequestCommand, Guid?>
{
    private readonly IProductRequestRepository _productRequestRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewProductRequestCommandHandler(
        IProductRequestRepository productRequestRepository,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResource> localizer,
        IUnitOfWork unitOfWork)
    {
        _productRequestRepository = productRequestRepository;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid?> Handle(ReviewProductRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.HasRole(UserRole.Admin, UserRole.SuperAdmin))
        {
            throw new ForbiddenAccessException(_localizer["UNAUTHORIZED_REVIEW_REQUESTS"]);
        }

        var productRequest = await _productRequestRepository.GetByIdAsync(request.ProductRequestId, cancellationToken);
        if (productRequest == null)
        {
            throw new NotFoundException(nameof(ProductRequest), request.ProductRequestId);
        }

        if (productRequest.Status != ApprovalStatus.Pending)
        {
            throw new BusinessRuleException("REQUEST_ALREADY_REVIEWED", _localizer["REQUEST_ALREADY_REVIEWED"]);
        }

        if (request.IsApproved)
        {
            productRequest.Approve();

            var slug = !string.IsNullOrWhiteSpace(productRequest.SuggestedNameEn)
                ? productRequest.SuggestedNameEn.ToLowerInvariant().Replace(" ", "-")
                : productRequest.SuggestedNameAr.Replace(" ", "-");

            var masterProduct = new MasterProduct(
                nameAr: productRequest.SuggestedNameAr,
                nameEn: productRequest.SuggestedNameEn,
                slug: slug,
                categoryId: productRequest.SuggestedCategoryId,
                descriptionAr: productRequest.SuggestedDescriptionAr,
                descriptionEn: productRequest.SuggestedDescriptionEn
            );

            if (!string.IsNullOrWhiteSpace(productRequest.ImageUrl))
            {
                masterProduct.AddImage(productRequest.ImageUrl, productRequest.SuggestedNameEn, 0, true);
            }

            _productRequestRepository.AddMasterProduct(masterProduct);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return masterProduct.Id;
        }

        if (string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            throw new BadRequestException("REJECTION_REASON_REQUIRED", _localizer["REJECTION_REASON_REQUIRED"]);
        }

        productRequest.Reject(request.RejectionReason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return null;
    }
}
