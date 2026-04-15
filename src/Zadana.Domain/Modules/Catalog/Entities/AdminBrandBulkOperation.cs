using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class AdminBrandBulkOperation : BaseEntity
{
    private readonly List<AdminBrandBulkOperationItem> _items = [];

    public Guid AdminUserId { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public AdminBrandBulkOperationStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    public int ProcessedRows { get; private set; }
    public int SucceededRows { get; private set; }
    public int FailedRows { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<AdminBrandBulkOperationItem> Items => _items;

    private AdminBrandBulkOperation() { }

    public AdminBrandBulkOperation(Guid adminUserId, string idempotencyKey, IEnumerable<AdminBrandBulkOperationItem> items)
    {
        AdminUserId = adminUserId;
        IdempotencyKey = idempotencyKey.Trim();
        Status = AdminBrandBulkOperationStatus.Pending;
        _items = items.ToList();
        TotalRows = _items.Count;
    }

    public void MarkProcessing()
    {
        Status = AdminBrandBulkOperationStatus.Processing;
        StartedAtUtc ??= DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = AdminBrandBulkOperationStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void RecalculateProgress()
    {
        ProcessedRows = _items.Count(x => x.Status != AdminBrandBulkOperationItemStatus.Pending);
        SucceededRows = _items.Count(x => x.Status == AdminBrandBulkOperationItemStatus.Succeeded);
        FailedRows = _items.Count(x => x.Status is AdminBrandBulkOperationItemStatus.Failed or AdminBrandBulkOperationItemStatus.Skipped);

        if (ProcessedRows < TotalRows)
        {
            Status = AdminBrandBulkOperationStatus.Processing;
            return;
        }

        CompletedAtUtc = DateTime.UtcNow;
        Status = FailedRows > 0
            ? AdminBrandBulkOperationStatus.CompletedWithErrors
            : AdminBrandBulkOperationStatus.Completed;
    }
}
