namespace Zadana.Application.Modules.Catalog.Common;

/// <summary>
/// Pricing ownership for vendor products across branches:
/// - First branch that adds a master product may set and later edit its price.
/// - Main/primary (or store-wide) scope may always edit price.
/// - Other branches inherit the locked price and can only manage stock/availability.
/// </summary>
public static class VendorProductPricingAuthority
{
    public static bool CanEditPrice(Guid? productBranchId, bool isPrimaryBranch, Guid? originBranchId)
    {
        if (productBranchId is null || isPrimaryBranch)
        {
            return true;
        }

        return Nullable.Equals(productBranchId, originBranchId);
    }

    public static bool PricesDiffer(
        decimal currentSelling,
        decimal? currentCompareAt,
        decimal? currentCost,
        decimal? currentTrade,
        decimal requestedSelling,
        decimal? requestedCompareAt,
        decimal? requestedCost,
        decimal? requestedTrade)
    {
        return currentSelling != requestedSelling
            || currentCompareAt != requestedCompareAt
            || currentCost != requestedCost
            || currentTrade != requestedTrade;
    }
}
