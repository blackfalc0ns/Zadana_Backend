using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class VendorProductBulkOperation : BaseEntity
{
    private readonly List<VendorProductBulkOperationItem> _items = [];

    public Guid VendorId { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public VendorProductBulkOperationStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    public int ProcessedRows { get; private set; }
    public int SucceededRows { get; private set; }
    public int FailedRows { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<VendorProductBulkOperationItem> Items => _items;

    private VendorProductBulkOperation() { }

    public VendorProductBulkOperation(Guid vendorId, string idempotencyKey, IEnumerable<VendorProductBulkOperationItem> items)
    {
        VendorId = vendorId;
        IdempotencyKey = idempotencyKey.Trim();
        Status = VendorProductBulkOperationStatus.Pending;
        _items = items.ToList();
        TotalRows = _items.Count;
    }

    public void MarkProcessing()
    {
        Status = VendorProductBulkOperationStatus.Processing;
        StartedAtUtc ??= DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = VendorProductBulkOperationStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void RecalculateProgress()
    {
        ProcessedRows = _items.Count(x => x.Status != VendorProductBulkOperationItemStatus.Pending);
        SucceededRows = _items.Count(x => x.Status == VendorProductBulkOperationItemStatus.Succeeded);
        FailedRows = _items.Count(x => x.Status is VendorProductBulkOperationItemStatus.Failed or VendorProductBulkOperationItemStatus.Skipped);

        if (ProcessedRows < TotalRows)
        {
            Status = VendorProductBulkOperationStatus.Processing;
            return;
        }

        CompletedAtUtc = DateTime.UtcNow;
        Status = FailedRows > 0
            ? VendorProductBulkOperationStatus.CompletedWithErrors
            : VendorProductBulkOperationStatus.Completed;
    }
}
