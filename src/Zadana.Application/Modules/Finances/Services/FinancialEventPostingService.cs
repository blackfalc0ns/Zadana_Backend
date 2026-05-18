using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Finances.Entities;
using Zadana.Domain.Modules.Finances.Enums;

namespace Zadana.Application.Modules.Finances.Services;

public sealed class FinancialEventPostingService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<FinancialEventPostingService> _logger;

    public FinancialEventPostingService(
        IApplicationDbContext context,
        ILogger<FinancialEventPostingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FinancialPostingResult> PostAsync(
        FinancialEventType eventType,
        string idempotencyKey,
        IReadOnlyCollection<JournalLineDraft> lines,
        Guid? orderId = null,
        Guid? settlementId = null,
        Guid? payoutId = null,
        Guid? refundId = null,
        string currencyCode = "SAR",
        Guid? correlationId = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        var existing = await FindExistingPostingAsync(normalizedIdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            return existing with { WasAlreadyPosted = true };
        }

        var eventRecord = new FinancialEvent(
            eventType,
            normalizedIdempotencyKey,
            orderId,
            settlementId,
            payoutId,
            refundId,
            currencyCode,
            correlationId,
            description: description);

        var sequenceNumber = await GetNextSequenceNumberAsync(cancellationToken);
        var journalEntry = new JournalEntry(eventRecord.Id, sequenceNumber, eventRecord.CurrencyCode, memo: description);

        foreach (var draft in lines)
        {
            journalEntry.AddLine(new JournalLine(
                journalEntry.Id,
                draft.AccountCode,
                draft.DebitAmount,
                draft.CreditAmount,
                eventRecord.CurrencyCode,
                draft.OwnerType,
                draft.OwnerId,
                draft.OrderId ?? orderId,
                draft.SettlementId ?? settlementId,
                draft.PayoutId ?? payoutId,
                draft.Memo));
        }

        journalEntry.EnsureBalanced();

        _context.FinancialEvents.Add(eventRecord);
        _context.JournalEntries.Add(journalEntry);
        _context.JournalLines.AddRange(journalEntry.Lines);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var duplicate = await FindExistingPostingAsync(normalizedIdempotencyKey, cancellationToken);
            if (duplicate is not null)
            {
                _logger.LogInformation(
                    "[FinancialPosting] Duplicate idempotency key {IdempotencyKey} resolved to existing journal entry {JournalEntryId}.",
                    normalizedIdempotencyKey,
                    duplicate.JournalEntryId);

                return duplicate with { WasAlreadyPosted = true };
            }

            throw;
        }

        return new FinancialPostingResult(eventRecord.Id, journalEntry.Id, sequenceNumber, false);
    }

    private async Task<FinancialPostingResult?> FindExistingPostingAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        return await _context.FinancialEvents
            .AsNoTracking()
            .Where(financialEvent => financialEvent.IdempotencyKey == idempotencyKey)
            .Join(
                _context.JournalEntries.AsNoTracking(),
                financialEvent => financialEvent.Id,
                journalEntry => journalEntry.FinancialEventId,
                (financialEvent, journalEntry) => new FinancialPostingResult(
                    financialEvent.Id,
                    journalEntry.Id,
                    journalEntry.SequenceNumber,
                    false))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<long> GetNextSequenceNumberAsync(CancellationToken cancellationToken)
    {
        var latest = await _context.JournalEntries
            .AsNoTracking()
            .OrderByDescending(entry => entry.SequenceNumber)
            .Select(entry => entry.SequenceNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return latest + 1;
    }

    private static string NormalizeIdempotencyKey(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        return idempotencyKey.Trim();
    }
}
