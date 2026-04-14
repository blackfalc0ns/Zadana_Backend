using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class VendorProductBulkOperationItem : BaseEntity
{
    public Guid OperationId { get; private set; }
    public int RowNumber { get; private set; }
    public Guid MasterProductId { get; private set; }
    public Guid? VendorBranchId { get; private set; }
    public decimal SellingPrice { get; private set; }
    public decimal? CompareAtPrice { get; private set; }
    public int StockQty { get; private set; }
    public string? Sku { get; private set; }
    public int MinOrderQty { get; private set; }
    public int? MaxOrderQty { get; private set; }
    public VendorProductBulkOperationItemStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? CreatedVendorProductId { get; private set; }

    public VendorProductBulkOperation Operation { get; private set; } = null!;
    public MasterProduct MasterProduct { get; private set; } = null!;
    public VendorBranch? VendorBranch { get; private set; }

    private VendorProductBulkOperationItem() { }

    public VendorProductBulkOperationItem(
        int rowNumber,
        Guid masterProductId,
        decimal sellingPrice,
        decimal? compareAtPrice,
        int stockQty,
        Guid? vendorBranchId,
        string? sku,
        int minOrderQty,
        int? maxOrderQty)
    {
        RowNumber = rowNumber;
        MasterProductId = masterProductId;
        SellingPrice = sellingPrice;
        CompareAtPrice = compareAtPrice;
        StockQty = stockQty;
        VendorBranchId = vendorBranchId;
        Sku = sku?.Trim();
        MinOrderQty = minOrderQty;
        MaxOrderQty = maxOrderQty;
        Status = VendorProductBulkOperationItemStatus.Pending;
    }

    public void AttachToOperation(Guid operationId)
    {
        OperationId = operationId;
    }

    public void MarkSucceeded(Guid vendorProductId)
    {
        Status = VendorProductBulkOperationItemStatus.Succeeded;
        CreatedVendorProductId = vendorProductId;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = VendorProductBulkOperationItemStatus.Failed;
        ErrorMessage = errorMessage;
    }

    public void MarkSkipped(string errorMessage)
    {
        Status = VendorProductBulkOperationItemStatus.Skipped;
        ErrorMessage = errorMessage;
    }
}
