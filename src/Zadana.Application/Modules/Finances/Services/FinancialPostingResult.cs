namespace Zadana.Application.Modules.Finances.Services;

public sealed record FinancialPostingResult(
    Guid FinancialEventId,
    Guid JournalEntryId,
    long SequenceNumber,
    bool WasAlreadyPosted);
