using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class AdminMasterProductBulkOperation : BaseEntity
{
    private readonly List<AdminMasterProductBulkOperationItem> _items = [];

    public Guid AdminUserId { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public AdminMasterProductBulkOperationStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    public int ProcessedRows { get; private set; }
    public int SucceededRows { get; private set; }
    public int FailedRows { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<AdminMasterProductBulkOperationItem> Items => _items;

    private AdminMasterProductBulkOperation() { }

    public AdminMasterProductBulkOperation(Guid adminUserId, string idempotencyKey, IEnumerable<AdminMasterProductBulkOperationItem> items)
    {
        AdminUserId = adminUserId;
        IdempotencyKey = idempotencyKey.Trim();
        Status = AdminMasterProductBulkOperationStatus.Pending;
        _items = items.ToList();
        TotalRows = _items.Count;
    }

    public void MarkProcessing()
    {
        Status = AdminMasterProductBulkOperationStatus.Processing;
        StartedAtUtc ??= DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = AdminMasterProductBulkOperationStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void RecalculateProgress()
    {
        ProcessedRows = _items.Count(x => x.Status != AdminMasterProductBulkOperationItemStatus.Pending);
        SucceededRows = _items.Count(x => x.Status == AdminMasterProductBulkOperationItemStatus.Succeeded);
        FailedRows = _items.Count(x => x.Status is AdminMasterProductBulkOperationItemStatus.Failed or AdminMasterProductBulkOperationItemStatus.Skipped);

        if (ProcessedRows < TotalRows)
        {
            Status = AdminMasterProductBulkOperationStatus.Processing;
            return;
        }

        CompletedAtUtc = DateTime.UtcNow;
        Status = FailedRows > 0
            ? AdminMasterProductBulkOperationStatus.CompletedWithErrors
            : AdminMasterProductBulkOperationStatus.Completed;
    }
}
