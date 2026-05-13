using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Marketing.Entities;

public class FeaturedProductSelectionSettings : BaseEntity
{
    public const int DefaultTargetCount = 10;
    public const int DefaultMinSalesCount = 1;
    public const int DefaultMinStoreCount = 2;
    public const bool DefaultRequireDiscount = false;
    public const bool DefaultExcludeProductsAlreadyInSpecialOffers = true;

    public FeaturedProductSelectionMode SelectionMode { get; private set; }
    public int TargetCount { get; private set; }
    public int MinSalesCount { get; private set; }
    public int MinStoreCount { get; private set; }
    public bool RequireDiscount { get; private set; }
    public bool ExcludeProductsAlreadyInSpecialOffers { get; private set; }

    private FeaturedProductSelectionSettings()
    {
    }

    public FeaturedProductSelectionSettings(
        FeaturedProductSelectionMode selectionMode,
        int targetCount = DefaultTargetCount,
        int minSalesCount = DefaultMinSalesCount,
        int minStoreCount = DefaultMinStoreCount,
        bool requireDiscount = DefaultRequireDiscount,
        bool excludeProductsAlreadyInSpecialOffers = DefaultExcludeProductsAlreadyInSpecialOffers)
    {
        Update(selectionMode, targetCount, minSalesCount, minStoreCount, requireDiscount, excludeProductsAlreadyInSpecialOffers);
    }

    public void Update(
        FeaturedProductSelectionMode selectionMode,
        int targetCount,
        int minSalesCount,
        int minStoreCount,
        bool requireDiscount,
        bool excludeProductsAlreadyInSpecialOffers)
    {
        if (targetCount < 0)
        {
            throw new BusinessRuleException("INVALID_FEATURED_TARGET_COUNT", "Featured products target count cannot be negative.");
        }

        if (minSalesCount < 0)
        {
            throw new BusinessRuleException("INVALID_FEATURED_MIN_SALES_COUNT", "Featured products minimum sales count cannot be negative.");
        }

        if (minStoreCount < 0)
        {
            throw new BusinessRuleException("INVALID_FEATURED_MIN_STORE_COUNT", "Featured products minimum store count cannot be negative.");
        }

        SelectionMode = selectionMode;
        TargetCount = targetCount;
        MinSalesCount = minSalesCount;
        MinStoreCount = minStoreCount;
        RequireDiscount = requireDiscount;
        ExcludeProductsAlreadyInSpecialOffers = excludeProductsAlreadyInSpecialOffers;
    }
}
