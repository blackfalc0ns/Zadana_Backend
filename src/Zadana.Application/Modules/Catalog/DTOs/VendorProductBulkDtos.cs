namespace Zadana.Application.Modules.Catalog.DTOs;

public record VendorProductBulkOperationItemDto(
    Guid Id,
    int RowNumber,
    Guid MasterProductId,
    string? ProductNameAr,
    string? ProductNameEn,
    decimal SellingPrice,
    decimal? CompareAtPrice,
    int StockQty,
    Guid? BranchId,
    string? Sku,
    int MinOrderQty,
    int? MaxOrderQty,
    string Status,
    string? ErrorMessage,
    Guid? CreatedVendorProductId);

public record VendorProductBulkOperationDto(
    Guid Id,
    string IdempotencyKey,
    string Status,
    int TotalRows,
    int ProcessedRows,
    int SucceededRows,
    int FailedRows,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc);

public record VendorProductBulkOperationDetailsDto(
    Guid Id,
    string IdempotencyKey,
    string Status,
    int TotalRows,
    int ProcessedRows,
    int SucceededRows,
    int FailedRows,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<VendorProductBulkOperationItemDto> Items);
