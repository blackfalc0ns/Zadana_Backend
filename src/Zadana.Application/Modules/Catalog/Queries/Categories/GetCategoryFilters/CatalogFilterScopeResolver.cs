namespace Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryFilters;

internal static class CatalogFilterScopeResolver
{
    public static CategoryFilterScope? Resolve(Guid categoryId, IReadOnlyCollection<CategoryScopeRow> categories)
    {
        var categoriesById = categories.ToDictionary(category => category.Id);
        if (!categoriesById.TryGetValue(categoryId, out var category) || !category.IsActive)
        {
            return null;
        }

        var activeChildrenByParent = categories
            .Where(child => child.IsActive && child.ParentCategoryId.HasValue)
            .GroupBy(child => child.ParentCategoryId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var activeSubtreeIds = new List<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(category.Id);

        while (stack.Count > 0)
        {
            var currentId = stack.Pop();
            activeSubtreeIds.Add(currentId);

            if (!activeChildrenByParent.TryGetValue(currentId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                stack.Push(child.Id);
            }
        }

        activeChildrenByParent.TryGetValue(category.Id, out var directChildren);

        return new CategoryFilterScope(
            category,
            activeSubtreeIds,
            directChildren ?? Array.Empty<CategoryScopeRow>());
    }
}

internal sealed record CategoryScopeRow(
    Guid Id,
    Guid? ParentCategoryId,
    string? NameAr,
    string? NameEn,
    int DisplayOrder,
    bool IsActive);

internal sealed record CategoryFilterScope(
    CategoryScopeRow Category,
    IReadOnlyList<Guid> ActiveSubtreeIds,
    IReadOnlyList<CategoryScopeRow> DirectActiveChildren);
