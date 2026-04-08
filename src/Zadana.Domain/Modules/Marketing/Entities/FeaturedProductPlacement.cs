using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Marketing.Entities;

public class FeaturedProductPlacement : BaseEntity
{
    public FeaturedPlacementType PlacementType { get; private set; }
    public Guid? VendorProductId { get; private set; }
    public Guid? MasterProductId { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }
    public string? Note { get; private set; }

    public VendorProduct? VendorProduct { get; private set; }
    public MasterProduct? MasterProduct { get; private set; }

    private FeaturedProductPlacement() { }

    public FeaturedProductPlacement(
        FeaturedPlacementType placementType,
        int displayOrder,
        Guid? vendorProductId = null,
        Guid? masterProductId = null,
        DateTime? startsAtUtc = null,
        DateTime? endsAtUtc = null,
        string? note = null)
    {
        ApplyState(placementType, displayOrder, vendorProductId, masterProductId, startsAtUtc, endsAtUtc, note);
        IsActive = true;
    }

    public void Update(
        FeaturedPlacementType placementType,
        int displayOrder,
        Guid? vendorProductId,
        Guid? masterProductId,
        DateTime? startsAtUtc,
        DateTime? endsAtUtc,
        string? note,
        bool isActive)
    {
        ApplyState(placementType, displayOrder, vendorProductId, masterProductId, startsAtUtc, endsAtUtc, note);
        IsActive = isActive;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    private void ApplyState(
        FeaturedPlacementType placementType,
        int displayOrder,
        Guid? vendorProductId,
        Guid? masterProductId,
        DateTime? startsAtUtc,
        DateTime? endsAtUtc,
        string? note)
    {
        if (displayOrder < 0)
        {
            throw new BusinessRuleException("INVALID_DISPLAY_ORDER", "Display order cannot be negative.");
        }

        if (startsAtUtc.HasValue && endsAtUtc.HasValue && endsAtUtc < startsAtUtc)
        {
            throw new BusinessRuleException("INVALID_DATE_RANGE", "EndsAtUtc must be greater than or equal to StartsAtUtc.");
        }

        var isVendorPlacement = placementType == FeaturedPlacementType.VendorProduct;
        if (isVendorPlacement && (!vendorProductId.HasValue || masterProductId.HasValue))
        {
            throw new BusinessRuleException("INVALID_FEATURED_PLACEMENT", "VendorProduct placements must reference only VendorProductId.");
        }

        if (!isVendorPlacement && (!masterProductId.HasValue || vendorProductId.HasValue))
        {
            throw new BusinessRuleException("INVALID_FEATURED_PLACEMENT", "MasterProduct placements must reference only MasterProductId.");
        }

        PlacementType = placementType;
        VendorProductId = vendorProductId;
        MasterProductId = masterProductId;
        DisplayOrder = displayOrder;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
