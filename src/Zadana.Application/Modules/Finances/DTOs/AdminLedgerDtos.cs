using Zadana.Domain.Modules.Finances.Enums;

namespace Zadana.Application.Modules.Finances.DTOs;

public sealed record AdminLedgerEntryListDto(
    IReadOnlyList<AdminLedgerEntryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminLedgerEntryDto(
    Guid Id,
    long SequenceNumber,
    JournalEntryStatus Status,
    FinancialEventType EventType,
    Guid CorrelationId,
    string IdempotencyKey,
    Guid? OrderId,
    Guid? SettlementId,
    Guid? PayoutId,
    Guid? RefundId,
    string CurrencyCode,
    DateTime PostedAtUtc,
    decimal DebitTotal,
    decimal CreditTotal,
    string? Memo,
    IReadOnlyList<AdminLedgerLineDto> Lines);

public sealed record AdminLedgerLineDto(
    Guid Id,
    FinancialAccountCode AccountCode,
    FinancialOwnerType? OwnerType,
    Guid? OwnerId,
    string? OwnerName,
    decimal DebitAmount,
    decimal CreditAmount,
    string CurrencyCode,
    Guid? OrderId,
    Guid? SettlementId,
    Guid? PayoutId,
    string? Memo);
