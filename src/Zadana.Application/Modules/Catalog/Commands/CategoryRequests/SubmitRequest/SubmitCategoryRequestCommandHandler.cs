using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Catalog.Common;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.CategoryRequests.SubmitRequest;

public class SubmitCategoryRequestCommandHandler : IRequestHandler<SubmitCategoryRequestCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentVendorService _currentVendorService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SubmitCategoryRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentVendorService currentVendorService,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _currentVendorService = currentVendorService;
        _localizer = localizer;
    }

    public async Task<Guid> Handle(SubmitCategoryRequestCommand request, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(cancellationToken)
            ?? throw new ForbiddenAccessException(_localizer["VENDOR_LOGIN_REQUIRED"]);

        if (!CategoryHierarchyRules.TryParseTargetLevel(request.TargetLevel, out var targetLevel))
        {
            throw new BusinessRuleException("INVALID_CATEGORY_TARGET_LEVEL", "Invalid category target level.");
        }

        if (!CategoryHierarchyRules.IsValidLevel(targetLevel))
        {
            throw new BusinessRuleException("CATEGORY_LEVEL_NOT_SUPPORTED", "Category requests cannot exceed the fourth level.");
        }

        if (!CategoryHierarchyRules.IsRequestTargetLevel(targetLevel))
        {
            throw new BusinessRuleException("CATEGORY_LEVEL_NOT_SUPPORTED", "Only category and sub-category requests are supported.");
        }

        var categories = await _context.Categories
            .AsNoTracking()
            .Select(category => new CategoryNode(category.Id, category.ParentCategoryId))
            .ToListAsync(cancellationToken);

        var categoryLookup = categories.ToDictionary(category => category.Id);

        if (request.ParentCategoryId.HasValue && !categoryLookup.ContainsKey(request.ParentCategoryId.Value))
        {
            throw new NotFoundException(nameof(Category), request.ParentCategoryId.Value);
        }

        if (!request.ParentCategoryId.HasValue)
        {
            throw new BusinessRuleException("CATEGORY_PARENT_REQUIRED", "This category level requires a parent category.");
        }

        if (request.ParentCategoryId.HasValue)
        {
            var parentLevel = ResolveLevel(request.ParentCategoryId.Value, categoryLookup);

            if (!CategoryHierarchyRules.IsAllowedParentLevel(targetLevel, parentLevel))
            {
                throw new BusinessRuleException("INVALID_CATEGORY_PARENT_LEVEL", "The selected parent category does not match the requested level.");
            }
        }

        var categoryRequest = new CategoryRequest(
            vendorId,
            request.NameAr,
            request.NameEn,
            CategoryHierarchyRules.ToKey(targetLevel),
            request.ParentCategoryId,
            request.DisplayOrder,
            request.ImageUrl);

        _context.CategoryRequests.Add(categoryRequest);
        await _context.SaveChangesAsync(cancellationToken);
        return categoryRequest.Id;
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
