using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
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
            var approvedParentCategoryId = await ResolveApprovedParentAsync(
                request.ApprovedTargetLevel,
                request.ApprovedParentCategoryId,
                categoryRequest.TargetLevel,
                categoryRequest.ParentCategoryId,
                cancellationToken);

            var category = new Category(
                categoryRequest.NameAr,
                categoryRequest.NameEn,
                categoryRequest.ImageUrl,
                approvedParentCategoryId,
                categoryRequest.DisplayOrder);

            _context.Categories.Add(category);
            categoryRequest.Approve(reviewerName, category.Id);
            _context.Notifications.Add(new Notification(
                categoryRequest.Vendor.UserId,
                "تمت الموافقة على طلب الكتالوج",
                "Catalog Request Approved",
                $"تمت الموافقة على طلب الفئة '{categoryRequest.NameAr}'.",
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
            "تم رفض طلب الكتالوج",
            "Catalog Request Rejected",
            $"تم رفض طلب الفئة '{categoryRequest.NameAr}'. السبب: {request.RejectionReason}",
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

    private async Task<Guid?> ResolveApprovedParentAsync(
        string? approvedTargetLevel,
        Guid? approvedParentCategoryId,
        string originalTargetLevel,
        Guid? originalParentCategoryId,
        CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Select(category => new CategoryNode(category.Id, category.ParentCategoryId))
            .ToListAsync(cancellationToken);

        var lookup = categories.ToDictionary(category => category.Id);

        if (!string.IsNullOrWhiteSpace(approvedTargetLevel))
        {
            if (!CategoryHierarchyRules.TryParseTargetLevel(approvedTargetLevel, out var parsedLevel))
            {
                throw new BusinessRuleException("INVALID_CATEGORY_TARGET_LEVEL", "Invalid category target level.");
            }

            ValidateParentLevel(parsedLevel, approvedParentCategoryId, lookup);
            return approvedParentCategoryId;
        }

        if (!CategoryHierarchyRules.TryParseTargetLevel(originalTargetLevel, out var originalParsedLevel))
        {
            throw new BusinessRuleException("INVALID_CATEGORY_TARGET_LEVEL", "Invalid category target level.");
        }

        if (!approvedParentCategoryId.HasValue)
        {
            ValidateParentLevel(originalParsedLevel, originalParentCategoryId, lookup);
            return originalParentCategoryId;
        }

        ValidateParentLevel(originalParsedLevel, approvedParentCategoryId, lookup);
        return approvedParentCategoryId;
    }

    private static void ValidateParentLevel(
        int targetLevel,
        Guid? parentCategoryId,
        IReadOnlyDictionary<Guid, CategoryNode> lookup)
    {
        if (!CategoryHierarchyRules.IsValidLevel(targetLevel))
        {
            throw new BusinessRuleException("CATEGORY_LEVEL_NOT_SUPPORTED", "Category requests cannot exceed the fourth level.");
        }

        if (!CategoryHierarchyRules.IsRequestTargetLevel(targetLevel))
        {
            throw new BusinessRuleException("CATEGORY_LEVEL_NOT_SUPPORTED", "Only category and sub-category requests are supported.");
        }

        if (!parentCategoryId.HasValue)
        {
            throw new BusinessRuleException("CATEGORY_PARENT_REQUIRED", "This category level requires a parent category.");
        }

        if (!lookup.ContainsKey(parentCategoryId.Value))
        {
            throw new NotFoundException(nameof(Category), parentCategoryId.Value);
        }

        var parentLevel = ResolveLevel(parentCategoryId.Value, lookup);
        if (!CategoryHierarchyRules.IsAllowedParentLevel(targetLevel, parentLevel))
        {
            throw new BusinessRuleException("INVALID_CATEGORY_PARENT_LEVEL", "The selected parent category does not match the requested level.");
        }
    }

    private static int ResolveLevel(Guid categoryId, IReadOnlyDictionary<Guid, CategoryNode> lookup)
    {
        var level = 0;
        var currentId = categoryId;

        while (lookup.TryGetValue(currentId, out var current) && current.ParentCategoryId.HasValue)
        {
            level++;

            if (level > CategoryHierarchyRules.MaxLevel)
            {
                throw new BusinessRuleException("CATEGORY_DEPTH_EXCEEDED", "Categories deeper than the supported hierarchy are not allowed.");
            }

            currentId = current.ParentCategoryId.Value;
        }

        return level;
    }

    private sealed record CategoryNode(Guid Id, Guid? ParentCategoryId);
}
