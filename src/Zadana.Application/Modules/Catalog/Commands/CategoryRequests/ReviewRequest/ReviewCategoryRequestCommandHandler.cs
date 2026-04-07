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

namespace Zadana.Application.Modules.Catalog.Commands.CategoryRequests.ReviewRequest;

public class ReviewCategoryRequestCommandHandler : IRequestHandler<ReviewCategoryRequestCommand, Guid?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ReviewCategoryRequestCommandHandler(
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

    public async Task<Guid?> Handle(ReviewCategoryRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.HasRole(UserRole.Admin, UserRole.SuperAdmin))
        {
            throw new ForbiddenAccessException(_localizer["UNAUTHORIZED_REVIEW_REQUESTS"]);
        }

        var categoryRequest = await _context.CategoryRequests
            .Include(x => x.Vendor)
            .FirstOrDefaultAsync(x => x.Id == request.CategoryRequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(CategoryRequest), request.CategoryRequestId);

        if (categoryRequest.Status != ApprovalStatus.Pending)
        {
            throw new BusinessRuleException("REQUEST_ALREADY_REVIEWED", _localizer["REQUEST_ALREADY_REVIEWED"]);
        }

        var reviewerName = await ResolveReviewerNameAsync(cancellationToken);

        if (request.IsApproved)
        {
            var category = new Category(
                categoryRequest.NameAr,
                categoryRequest.NameEn,
                categoryRequest.ImageUrl,
                categoryRequest.ParentCategoryId,
                categoryRequest.DisplayOrder);

            _context.Categories.Add(category);
            categoryRequest.Approve(reviewerName, category.Id);
            _context.Notifications.Add(new Notification(
                categoryRequest.Vendor.UserId,
                "Catalog Request Approved",
                $"Your category request '{categoryRequest.NameEn}' has been approved.",
                "catalog_request_category"));
            await _context.SaveChangesAsync(cancellationToken);
            return category.Id;
        }

        if (string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            throw new BadRequestException("REJECTION_REASON_REQUIRED", _localizer["REJECTION_REASON_REQUIRED"]);
        }

        categoryRequest.Reject(request.RejectionReason, reviewerName);
        _context.Notifications.Add(new Notification(
            categoryRequest.Vendor.UserId,
            "Catalog Request Rejected",
            $"Your category request '{categoryRequest.NameEn}' was rejected. Reason: {request.RejectionReason}",
            "catalog_request_category"));
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
