using MediatR;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.Categories.UpdateCategory;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateCategoryCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }


    public async Task Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories.FindAsync(new object[] { request.Id }, cancellationToken);
        if (category == null)
            throw new NotFoundException(nameof(Category), request.Id);

        if (request.ParentCategoryId.HasValue)
        {
            var parentExists = await _context.Categories.FindAsync(new object[] { request.ParentCategoryId.Value }, cancellationToken);
            if (parentExists == null)
                throw new NotFoundException(nameof(Category), request.ParentCategoryId.Value);

            // Guard: prevent circular references (A → B → C → A)
            var currentParentId = request.ParentCategoryId.Value;
            var visited = new HashSet<Guid> { request.Id };
            while (currentParentId != Guid.Empty)
            {
                if (visited.Contains(currentParentId))
                {
                    throw new BusinessRuleException(
                        "CATEGORY_CIRCULAR_REFERENCE",
                        "لا يمكن تعيين هذا التصنيف كأب لأنه يسبب حلقة مرجعية.|Cannot set this parent category because it would create a circular reference.");
                }
                visited.Add(currentParentId);
                var parent = await _context.Categories.FindAsync(new object[] { currentParentId }, cancellationToken);
                currentParentId = parent?.ParentCategoryId ?? Guid.Empty;
            }
        }

        category.Update(request.NameAr, request.NameEn, request.ImageUrl, request.ParentCategoryId, request.DisplayOrder);

        if (request.IsActive && !category.IsActive)
            category.Activate();
        else if (!request.IsActive && category.IsActive)
            category.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
    }
}
