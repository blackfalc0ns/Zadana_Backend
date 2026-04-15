namespace Zadana.Application.Modules.Catalog.DTOs;

public record AdminBrandBulkOperationItemDto(
    Guid Id,
    int RowNumber,
    string NameAr,
    string NameEn,
    string? LogoUrl,
    Guid CategoryId,
    bool IsActive,
    string Status,
    string? ErrorMessage,
    Guid? CreatedBrandId);

public record AdminBrandBulkOperationDto(
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
