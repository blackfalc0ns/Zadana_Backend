namespace Zadana.Application.Modules.Catalog.Common;

public static class CategoryHierarchyRules
{
    public const int ActivityLevel = 0;
    public const int SubActivityLevel = 1;
    public const int CategoryLevel = 2;
    public const int SubCategoryLevel = 3;
    public const int MaxLevel = SubCategoryLevel;

    public static bool TryParseTargetLevel(string? value, out int level)
    {
        level = ActivityLevel;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "activity":
                level = ActivityLevel;
                return true;
            case "sub_activity":
                level = SubActivityLevel;
                return true;
            case "category":
                level = CategoryLevel;
                return true;
            case "sub_category":
                level = SubCategoryLevel;
                return true;
            default:
                return false;
        }
    }

    public static string ToKey(int level) =>
        level switch
        {
            ActivityLevel => "activity",
            SubActivityLevel => "sub_activity",
            CategoryLevel => "category",
            SubCategoryLevel => "sub_category",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported category hierarchy level.")
        };

    public static bool IsRequestTargetLevel(int level) =>
        level is CategoryLevel or SubCategoryLevel;

    public static int? GetExpectedParentLevel(int targetLevel) =>
        targetLevel == ActivityLevel ? null : targetLevel - 1;

    public static bool IsAllowedParentLevel(int targetLevel, int? parentLevel) =>
        targetLevel switch
        {
            CategoryLevel => parentLevel is ActivityLevel or SubActivityLevel,
            SubCategoryLevel => parentLevel == CategoryLevel,
            _ => false
        };

    public static bool IsValidLevel(int level) => level >= ActivityLevel && level <= MaxLevel;
}
