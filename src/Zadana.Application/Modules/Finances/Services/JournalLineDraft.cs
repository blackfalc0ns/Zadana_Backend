using Zadana.Domain.Modules.Finances.Enums;

namespace Zadana.Application.Modules.Finances.Services;

public sealed record JournalLineDraft(
    FinancialAccountCode AccountCode,
    decimal DebitAmount,
    decimal CreditAmount,
    FinancialOwnerType? OwnerType = null,
    Guid? OwnerId = null,
    Guid? OrderId = null,
    Guid? SettlementId = null,
    Guid? PayoutId = null,
    string? Memo = null);
