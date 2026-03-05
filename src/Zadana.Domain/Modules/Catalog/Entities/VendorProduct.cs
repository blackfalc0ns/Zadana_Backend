using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class VendorProduct : BaseEntity
{
    public Guid VendorId { get; private set; }
    public Guid MasterProductId { get; private set; }
    public Guid? VendorBranchId { get; private set; }
    
    // Vendor Custom Overrides
    public string? CustomNameAr { get; private set; }
    public string? CustomNameEn { get; private set; }
    public string? CustomDescriptionAr { get; private set; }
    public string? CustomDescriptionEn { get; private set; }

    
    public decimal SellingPrice { get; private set; }
    public decimal? CompareAtPrice { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsAvailable { get; private set; }
    public VendorProductStatus Status { get; private set; }

    // Navigation
    public Vendor Vendor { get; private set; } = null!;
    public MasterProduct MasterProduct { get; private set; } = null!;
    public VendorBranch? VendorBranch { get; private set; }

    private VendorProduct() { }

    public VendorProduct(
        Guid vendorId,
        Guid masterProductId,
        decimal sellingPrice,
        int stockQuantity = 0,
        decimal? compareAtPrice = null,
        Guid? vendorBranchId = null,
        string? customNameAr = null,
        string? customNameEn = null,
        string? customDescriptionAr = null,
        string? customDescriptionEn = null)
    {
        if (sellingPrice < 0)
            throw new BusinessRuleException("INVALID_PRICE", "Selling price cannot be negative.");
            
        if (stockQuantity < 0)
            throw new BusinessRuleException("INVALID_STOCK", "Stock quantity cannot be negative.");

        VendorId = vendorId;
        MasterProductId = masterProductId;
        VendorBranchId = vendorBranchId;
        SellingPrice = sellingPrice;
        CompareAtPrice = compareAtPrice;
        StockQuantity = stockQuantity;
        IsAvailable = stockQuantity > 0;
        Status = VendorProductStatus.Active;
        
        CustomNameAr = customNameAr?.Trim();
        CustomNameEn = customNameEn?.Trim();
        CustomDescriptionAr = customDescriptionAr?.Trim();
        CustomDescriptionEn = customDescriptionEn?.Trim();
    }

    public void UpdatePricing(decimal sellingPrice, decimal? compareAtPrice)
    {
        if (sellingPrice < 0)
            throw new BusinessRuleException("INVALID_PRICE", "Selling price cannot be negative.");

        SellingPrice = sellingPrice;
        CompareAtPrice = compareAtPrice;
    }

    public void UpdateStock(int quantity)
    {
        if (quantity < 0)
            throw new BusinessRuleException("INVALID_STOCK", "Stock quantity cannot be negative.");

        StockQuantity = quantity;
        IsAvailable = quantity > 0;
        
        if (quantity == 0 && Status == VendorProductStatus.Active)
        {
            Status = VendorProductStatus.OutOfStock;
        }
        else if (quantity > 0 && Status == VendorProductStatus.OutOfStock)
        {
            Status = VendorProductStatus.Active;
        }
    }

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;

    public void UpdateCustomDetails(string? customNameAr, string? customNameEn, string? customDescriptionAr, string? customDescriptionEn)
    {
        CustomNameAr = customNameAr?.Trim();
        CustomNameEn = customNameEn?.Trim();
        CustomDescriptionAr = customDescriptionAr?.Trim();
        CustomDescriptionEn = customDescriptionEn?.Trim();
    }

    public void Suspend() => Status = VendorProductStatus.Suspended;
    public void Activate() => Status = VendorProductStatus.Active;
}
