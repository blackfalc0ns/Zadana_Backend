using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Finances.Services;

/// <summary>
/// Imports an outbound bank-statement CSV and matches its rows to payouts using
/// bank reference plus amount. A match is an audit/reconciliation signal only;
/// it never changes a payout to Paid or bypasses the proof/approval workflow.
/// </summary>
public sealed class PayoutBankReconciliationService
{
    private const int MaxCsvBytes = 5 * 1024 * 1024;
    private readonly IApplicationDbContext _context;

    public PayoutBankReconciliationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PayoutBankStatementImportResult> ImportAsync(
        Stream csv,
        string fileName,
        Guid importedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (csv is null || !csv.CanRead)
        {
            throw new BusinessRuleException("BANK_STATEMENT_FILE_REQUIRED", "A readable bank statement CSV file is required.");
        }

        if (importedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CONFIRMING_USER_REQUIRED", "An authenticated finance administrator is required.");
        }

        var bytes = await ReadLimitedAsync(csv, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (await _context.PayoutBankStatementImports
                .AsNoTracking()
                .AnyAsync(item => item.FileSha256 == hash, cancellationToken))
        {
            throw new BusinessRuleException(
                "BANK_STATEMENT_ALREADY_IMPORTED",
                "This exact bank statement was already imported. Use its reconciliation queue instead of importing it again.");
        }

        var rows = ParseCsv(DecodeUtf8(bytes));
        if (rows.Count < 2)
        {
            throw new BusinessRuleException("BANK_STATEMENT_EMPTY", "The bank statement must include a header and at least one data row.");
        }

        var columns = ResolveColumns(rows[0]);
        var import = new PayoutBankStatementImport(fileName, hash, importedByUserId);
        _context.PayoutBankStatementImports.Add(import);

        var candidates = await LoadCandidatePayoutsAsync(cancellationToken);
        var alreadyMatchedPayoutIds = await _context.PayoutBankStatementEntries
            .AsNoTracking()
            .Where(item => item.Status == PayoutBankStatementEntryStatus.Matched && item.PayoutId.HasValue)
            .Select(item => item.PayoutId!.Value)
            .ToHashSetAsync(cancellationToken);

        var matchedInThisImport = new HashSet<Guid>();
        var totalRows = 0;
        var matchedRows = 0;
        var unmatchedRows = 0;
        var ambiguousRows = 0;
        var mismatchRows = 0;
        var invalidRows = 0;

        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            totalRows++;
            if (!TryCreateStatementEntry(import.Id, index + 1, row, columns, out var entry, out var invalidReason))
            {
                invalidRows++;
                continue;
            }

            var referenceCandidates = candidates
                .Where(candidate => candidate.NormalizedReferences.Contains(entry.NormalizedBankReference))
                .ToList();
            var amountCandidates = referenceCandidates
                .Where(candidate => candidate.Amount == entry.Amount)
                .Where(candidate => !alreadyMatchedPayoutIds.Contains(candidate.Id) && !matchedInThisImport.Contains(candidate.Id))
                .ToList();

            if (amountCandidates.Count == 1)
            {
                entry.Match(amountCandidates[0].Id, importedByUserId, "Matched automatically by bank reference and amount.");
                matchedInThisImport.Add(amountCandidates[0].Id);
                matchedRows++;
            }
            else if (amountCandidates.Count > 1)
            {
                entry.MarkAmbiguous("More than one payout has the same bank reference and amount.");
                ambiguousRows++;
            }
            else if (referenceCandidates.Count > 0)
            {
                entry.MarkMismatch(
                    alreadyMatchedPayoutIds.Intersect(referenceCandidates.Select(candidate => candidate.Id)).Any()
                        ? "The referenced payout was already matched by another statement row."
                        : "A payout with this bank reference exists, but its amount does not match this statement row.");
                mismatchRows++;
            }
            else
            {
                unmatchedRows++;
            }

            _context.PayoutBankStatementEntries.Add(entry);
        }

        import.SetSummary(totalRows, matchedRows, unmatchedRows, ambiguousRows, mismatchRows, invalidRows);
        await _context.SaveChangesAsync(cancellationToken);

        return new PayoutBankStatementImportResult(
            import.Id,
            import.FileName,
            import.ImportedAtUtc,
            totalRows,
            matchedRows,
            unmatchedRows,
            ambiguousRows,
            mismatchRows,
            invalidRows);
    }

    public async Task<PayoutBankStatementEntry> MatchAsync(
        Guid entryId,
        Guid payoutId,
        Guid matchedByUserId,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        if (payoutId == Guid.Empty || matchedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_REQUIRED", "A payout and authenticated finance administrator are required for matching.");
        }

        var entry = await _context.PayoutBankStatementEntries
            .FirstOrDefaultAsync(item => item.Id == entryId, cancellationToken)
            ?? throw new NotFoundException("PayoutBankStatementEntry", entryId);

        if (entry.Status == PayoutBankStatementEntryStatus.Ignored)
        {
            throw new BusinessRuleException("BANK_STATEMENT_ENTRY_IGNORED", "An ignored bank statement row cannot be matched.");
        }

        // HTTP retries for the same successful decision are safe, but a
        // completed reconciliation row is otherwise immutable. Reassigning it
        // would erase the original reviewer and timestamp without any audit
        // trail, so it must go through a future explicit correction flow.
        if (entry.Status == PayoutBankStatementEntryStatus.Matched)
        {
            if (entry.PayoutId == payoutId)
            {
                return entry;
            }

            throw new BusinessRuleException(
                "BANK_STATEMENT_ENTRY_ALREADY_RESOLVED",
                "This bank statement row is already matched to another payout and cannot be reassigned.");
        }

        var payout = await _context.Payouts
            .AsNoTracking()
            .Include(item => item.ManualConfirmation)
            .Include(item => item.ExecutionReservation)
            .FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken)
            ?? throw new NotFoundException("Payout", payoutId);

        if (payout.Amount != entry.Amount)
        {
            throw new BusinessRuleException("BANK_STATEMENT_AMOUNT_MISMATCH", "The selected payout amount does not match the bank statement row.");
        }

        var normalizedReferences = BuildNormalizedReferences(payout);
        if (!normalizedReferences.Contains(entry.NormalizedBankReference))
        {
            throw new BusinessRuleException("BANK_STATEMENT_REFERENCE_MISMATCH", "The selected payout does not have the same bank reference as this statement row.");
        }

        var alreadyMatched = await _context.PayoutBankStatementEntries
            .AsNoTracking()
            .AnyAsync(
                item => item.PayoutId == payoutId && item.Id != entryId && item.Status == PayoutBankStatementEntryStatus.Matched,
                cancellationToken);
        if (alreadyMatched)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_RECONCILED", "This payout is already matched to a different bank statement row.");
        }

        entry.Match(payoutId, matchedByUserId, note ?? "Matched manually by finance.");
        await RefreshImportSummaryAsync(entry.ImportId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<PayoutBankStatementEntry> IgnoreAsync(
        Guid entryId,
        Guid resolvedByUserId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var entry = await _context.PayoutBankStatementEntries
            .FirstOrDefaultAsync(item => item.Id == entryId, cancellationToken)
            ?? throw new NotFoundException("PayoutBankStatementEntry", entryId);

        if (entry.Status == PayoutBankStatementEntryStatus.Ignored)
        {
            return entry;
        }

        if (entry.Status == PayoutBankStatementEntryStatus.Matched)
        {
            throw new BusinessRuleException(
                "BANK_STATEMENT_ENTRY_ALREADY_RESOLVED",
                "A matched bank statement row cannot be ignored or changed.");
        }

        entry.MarkIgnored(resolvedByUserId, note ?? "Resolved outside payout reconciliation.");
        await RefreshImportSummaryAsync(entry.ImportId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    private async Task<List<PayoutReconciliationCandidate>> LoadCandidatePayoutsAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddYears(-2);
        var payouts = await _context.Payouts
            .AsNoTracking()
            .Include(item => item.ManualConfirmation)
            .Include(item => item.ExecutionReservation)
            .Where(item => item.CreatedAtUtc >= cutoff)
            .Select(item => new
            {
                item.Id,
                item.Amount,
                item.TransferReference,
                ManualReference = item.ManualConfirmation == null ? null : item.ManualConfirmation.TransferReference,
                SubmissionReference = item.ExecutionReservation == null ? null : item.ExecutionReservation.SubmissionReference
            })
            .ToListAsync(cancellationToken);

        return payouts.Select(item => new PayoutReconciliationCandidate(
                item.Id,
                item.Amount,
                NormalizeReferences(item.TransferReference, item.ManualReference, item.SubmissionReference)))
            .Where(item => item.NormalizedReferences.Count > 0)
            .ToList();
    }

    private async Task RefreshImportSummaryAsync(Guid importId, CancellationToken cancellationToken)
    {
        var import = await _context.PayoutBankStatementImports
            .Include(item => item.Entries)
            .FirstOrDefaultAsync(item => item.Id == importId, cancellationToken)
            ?? throw new NotFoundException("PayoutBankStatementImport", importId);

        import.SetSummary(
            import.Entries.Count,
            import.Entries.Count(item => item.Status == PayoutBankStatementEntryStatus.Matched),
            import.Entries.Count(item => item.Status == PayoutBankStatementEntryStatus.Unmatched),
            import.Entries.Count(item => item.Status == PayoutBankStatementEntryStatus.Ambiguous),
            import.Entries.Count(item => item.Status == PayoutBankStatementEntryStatus.Mismatch),
            import.InvalidRows);
    }

    private static bool TryCreateStatementEntry(
        Guid importId,
        int rowNumber,
        IReadOnlyList<string> row,
        CsvColumns columns,
        out PayoutBankStatementEntry entry,
        out string? reason)
    {
        entry = default!;
        reason = null;
        var reference = GetValue(row, columns.Reference);
        var normalizedReference = NormalizeReference(reference);
        if (string.IsNullOrWhiteSpace(normalizedReference))
        {
            reason = "Missing bank reference.";
            return false;
        }

        if (!TryParseAmount(GetValue(row, columns.Amount), out var amount))
        {
            reason = "Invalid outgoing amount.";
            return false;
        }

        if (!TryParseDate(GetValue(row, columns.TransactionDate), out var transactionDate))
        {
            reason = "Invalid transaction date.";
            return false;
        }

        entry = new PayoutBankStatementEntry(
            importId,
            rowNumber,
            reference!,
            normalizedReference,
            amount,
            transactionDate,
            MaskBeneficiary(GetValue(row, columns.Beneficiary)),
            // Statement descriptions frequently contain names, account numbers
            // or free-form bank data. Matching uses only reference + amount,
            // so do not retain the raw memo as a second unencrypted PII store.
            null,
            GetValue(row, columns.Currency) ?? "SAR");
        return true;
    }

    private static CsvColumns ResolveColumns(IReadOnlyList<string> headers)
    {
        var normalized = headers
            .Select((value, index) => new { Name = NormalizeHeader(value), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        var reference = FindColumn(normalized, "reference", "bankreference", "transactionreference", "referencenumber", "المرجع", "رقمالمرجع");
        var amount = FindColumn(normalized, "amount", "transactionamount", "debitamount", "المبلغ");
        var transactionDate = FindColumn(normalized, "transactiondate", "date", "valuedate", "تاريخ", "تاريخالتحويل");
        if (reference < 0 || amount < 0 || transactionDate < 0)
        {
            throw new BusinessRuleException(
                "BANK_STATEMENT_COLUMNS_INVALID",
                "CSV must contain reference, amount, and transactionDate columns.");
        }

        return new CsvColumns(
            reference,
            amount,
            transactionDate,
            FindColumn(normalized, "beneficiary", "beneficiaryname", "المستفيد", "اسمالمستفيد"),
            FindColumn(normalized, "memo", "description", "narrative", "البيان", "الوصف"),
            FindColumn(normalized, "currency", "currencycode", "العملة"));
    }

    private static int FindColumn(IReadOnlyDictionary<string, int> headers, params string[] names)
    {
        foreach (var name in names)
        {
            if (headers.TryGetValue(name, out var index))
            {
                return index;
            }
        }

        return -1;
    }

    private static string? GetValue(IReadOnlyList<string> row, int index) =>
        index < 0 || index >= row.Count ? null : row[index]?.Trim();

    public static string NormalizeReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return string.Empty;
        }

        var ascii = ConvertArabicIndicDigits(reference.Trim());
        return new string(ascii.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    private static HashSet<string> NormalizeReferences(params string?[] references) =>
        references
            .Select(NormalizeReference)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> BuildNormalizedReferences(Payout payout) =>
        NormalizeReferences(
            payout.TransferReference,
            payout.ManualConfirmation?.TransferReference,
            payout.ExecutionReservation?.SubmissionReference);

    private static async Task<byte[]> ReadLimitedAsync(Stream source, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = await source.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxCsvBytes)
            {
                throw new BusinessRuleException("BANK_STATEMENT_TOO_LARGE", "Bank statement CSV cannot exceed 5 MB.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (buffer.Length == 0)
        {
            throw new BusinessRuleException("BANK_STATEMENT_EMPTY", "Bank statement CSV is empty.");
        }

        return buffer.ToArray();
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes)
                .TrimStart('\uFEFF');
        }
        catch (DecoderFallbackException)
        {
            throw new BusinessRuleException("BANK_STATEMENT_ENCODING_INVALID", "Bank statement CSV must be UTF-8 encoded.");
        }
    }

    private static List<IReadOnlyList<string>> ParseCsv(string content)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < content.Length; index++)
        {
            var current = content[index];
            if (current == '"')
            {
                if (quoted && index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }

                continue;
            }

            if (!quoted && current == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (!quoted && (current == '\r' || current == '\n'))
            {
                if (current == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
                continue;
            }

            field.Append(current);
        }

        if (quoted)
        {
            throw new BusinessRuleException("BANK_STATEMENT_CSV_INVALID", "Bank statement CSV contains an unclosed quoted value.");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static bool TryParseAmount(string? raw, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = ConvertArabicIndicDigits(raw)
            .Replace("SAR", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("ر.س", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount) &&
               amount > 0m;
    }

    private static bool TryParseDate(string? raw, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var formats = new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ" };
        return DateTime.TryParseExact(
                   ConvertArabicIndicDigits(raw.Trim()),
                   formats,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                   out date) ||
               DateTime.TryParse(raw, CultureInfo.GetCultureInfo("ar-SA"), DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out date);
    }

    private static string NormalizeHeader(string? value) =>
        new string((value ?? string.Empty)
            .Trim()
            .TrimStart('\uFEFF')
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();

    private static string ConvertArabicIndicDigits(string value)
    {
        var chars = value.ToCharArray();
        for (var index = 0; index < chars.Length; index++)
        {
            chars[index] = chars[index] switch
            {
                >= '\u0660' and <= '\u0669' => (char)('0' + chars[index] - '\u0660'),
                >= '\u06F0' and <= '\u06F9' => (char)('0' + chars[index] - '\u06F0'),
                _ => chars[index]
            };
        }

        return new string(chars);
    }

    private static string? MaskBeneficiary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return "****";
        }

        return $"{trimmed[..2]}***{trimmed[^2..]}";
    }

    private sealed record CsvColumns(
        int Reference,
        int Amount,
        int TransactionDate,
        int Beneficiary,
        int Memo,
        int Currency);

    private sealed record PayoutReconciliationCandidate(
        Guid Id,
        decimal Amount,
        HashSet<string> NormalizedReferences);
}

public sealed record PayoutBankStatementImportResult(
    Guid ImportId,
    string FileName,
    DateTime ImportedAtUtc,
    int TotalRows,
    int MatchedRows,
    int UnmatchedRows,
    int AmbiguousRows,
    int MismatchRows,
    int InvalidRows);
