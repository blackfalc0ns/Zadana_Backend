namespace Zadana.Application.Modules.Catalog.DTOs;

public record AdminMasterProductBulkOperationItemDto(
    Guid Id,
    int RowNumber,
    string NameAr,
    string NameEn,
    string Slug,
    string? Barcode,
    Guid CategoryId,
    Guid? BrandId,
    Guid? UnitId,
    string StatusValue,
    string? DescriptionAr,
    string? DescriptionEn,
    string Status,
    string? ErrorMessage,
    Guid? CreatedMasterProductId);

public record AdminMasterProductBulkOperationDto(
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
