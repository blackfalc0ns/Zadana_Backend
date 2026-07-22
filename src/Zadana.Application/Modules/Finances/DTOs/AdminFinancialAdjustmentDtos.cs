namespace Zadana.Application.Modules.Finances.DTOs;

public sealed record AdminFinancialAdjustmentListDto(
    IReadOnlyList<AdminFinancialAdjustmentDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminFinancialAdjustmentDto(
    Guid Id,
    string OwnerType,
    Guid OwnerId,
    string? OwnerName,
    decimal Amount,
    string Direction,
    string? Description,
    DateTime CreatedAtUtc);

public sealed record CreateAdminFinancialAdjustmentRequest(
    string OwnerType,
    Guid OwnerId,
    decimal Amount,
    string Direction,
    string? Reason,
    string? Category);
