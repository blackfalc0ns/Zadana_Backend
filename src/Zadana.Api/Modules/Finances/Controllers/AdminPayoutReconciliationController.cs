using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Finances.Controllers;

/// <summary>
/// Finance-only reconciliation for outbound bank transfers. Importing and
/// matching a statement row is deliberately an audit operation: it never
/// changes a payout to paid and can never bypass the protected proof/approval
/// flow.
/// </summary>
[ApiController]
[Route("api/admin/payout-reconciliation")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminPayoutReconciliationController(
    IApplicationDbContext context,
    PayoutBankReconciliationService reconciliationService,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    private const long MaxCsvRequestBytes = 5L * 1024 * 1024 + 16 * 1024;

    [HttpGet("imports")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PayoutBankStatementImportListDto>> GetImports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = context.PayoutBankStatementImports.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.ImportedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new PayoutBankStatementImportDto(
                item.Id,
                item.FileName,
                item.ImportedByUserId,
                item.ImportedAtUtc,
                item.TotalRows,
                item.MatchedRows,
                item.UnmatchedRows,
                item.AmbiguousRows,
                item.MismatchRows,
                item.InvalidRows))
            .ToListAsync(cancellationToken);

        return Ok(new PayoutBankStatementImportListDto(items, page, pageSize, totalCount));
    }

    [HttpGet("imports/{id:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PayoutBankStatementImportDetailDto>> GetImport(
        Guid id,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var import = await context.PayoutBankStatementImports
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (import is null)
        {
            return NotFound();
        }

        var entries = context.PayoutBankStatementEntries
            .AsNoTracking()
            .Where(item => item.ImportId == id);
        if (Enum.TryParse<PayoutBankStatementEntryStatus>(status, true, out var parsedStatus))
        {
            entries = entries.Where(item => item.Status == parsedStatus);
        }

        var entryTotal = await entries.CountAsync(cancellationToken);
        var items = await entries
            .OrderByDescending(item => item.TransactionDateUtc)
            .ThenByDescending(item => item.RowNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new PayoutBankStatementEntryDto(
                item.Id,
                item.ImportId,
                item.RowNumber,
                item.BankReference,
                item.Amount,
                item.TransactionDateUtc,
                item.CurrencyCode,
                item.BeneficiaryMasked,
                item.Status.ToString(),
                item.PayoutId,
                item.MatchedByUserId,
                item.MatchedAtUtc,
                item.ResolutionNote))
            .ToListAsync(cancellationToken);

        return Ok(new PayoutBankStatementImportDetailDto(
            ToImportDto(import),
            new PayoutBankStatementEntryListDto(items, page, pageSize, entryTotal)));
    }

    [HttpGet("entries")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PayoutBankStatementEntryListDto>> GetEntries(
        [FromQuery] Guid? importId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var entries = context.PayoutBankStatementEntries.AsNoTracking().AsQueryable();
        if (importId.HasValue)
        {
            entries = entries.Where(item => item.ImportId == importId.Value);
        }

        if (Enum.TryParse<PayoutBankStatementEntryStatus>(status, true, out var parsedStatus))
        {
            entries = entries.Where(item => item.Status == parsedStatus);
        }

        var totalCount = await entries.CountAsync(cancellationToken);
        var items = await entries
            .OrderByDescending(item => item.TransactionDateUtc)
            .ThenByDescending(item => item.RowNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new PayoutBankStatementEntryDto(
                item.Id,
                item.ImportId,
                item.RowNumber,
                item.BankReference,
                item.Amount,
                item.TransactionDateUtc,
                item.CurrencyCode,
                item.BeneficiaryMasked,
                item.Status.ToString(),
                item.PayoutId,
                item.MatchedByUserId,
                item.MatchedAtUtc,
                item.ResolutionNote))
            .ToListAsync(cancellationToken);

        return Ok(new PayoutBankStatementEntryListDto(items, page, pageSize, totalCount));
    }

    [HttpPost("imports")]
    [RequestSizeLimit(MaxCsvRequestBytes)]
    public async Task<ActionResult<PayoutBankStatementImportDto>> ImportBankStatement(
        [FromForm] IFormFile statement,
        CancellationToken cancellationToken = default)
    {
        if (statement is null || statement.Length == 0)
        {
            return BadRequest(new { error = "BANK_STATEMENT_FILE_REQUIRED" });
        }

        if (statement.Length > MaxCsvRequestBytes ||
            !string.Equals(Path.GetExtension(statement.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "BANK_STATEMENT_CSV_REQUIRED" });
        }

        var actorUserId = currentUserService.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        await using var stream = statement.OpenReadStream();
        var result = await reconciliationService.ImportAsync(
            stream,
            statement.FileName,
            actorUserId,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetImport),
            new { id = result.ImportId },
            new PayoutBankStatementImportDto(
                result.ImportId,
                result.FileName,
                actorUserId,
                result.ImportedAtUtc,
                result.TotalRows,
                result.MatchedRows,
                result.UnmatchedRows,
                result.AmbiguousRows,
                result.MismatchRows,
                result.InvalidRows));
    }

    [HttpPost("entries/{entryId:guid}/match")]
    public async Task<ActionResult<PayoutBankStatementEntryDto>> MatchEntry(
        Guid entryId,
        [FromBody] MatchPayoutBankStatementEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.PayoutId == Guid.Empty)
        {
            return BadRequest(new { error = "PAYOUT_REQUIRED" });
        }

        var actorUserId = currentUserService.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var entry = await reconciliationService.MatchAsync(
            entryId,
            request.PayoutId,
            actorUserId,
            request.Note,
            cancellationToken);
        return Ok(ToEntryDto(entry));
    }

    [HttpPost("entries/{entryId:guid}/ignore")]
    public async Task<ActionResult<PayoutBankStatementEntryDto>> IgnoreEntry(
        Guid entryId,
        [FromBody] IgnorePayoutBankStatementEntryRequest? request,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = currentUserService.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var entry = await reconciliationService.IgnoreAsync(
            entryId,
            actorUserId,
            request?.Note,
            cancellationToken);
        return Ok(ToEntryDto(entry));
    }

    private static PayoutBankStatementImportDto ToImportDto(
        Zadana.Domain.Modules.Wallets.Entities.PayoutBankStatementImport item) =>
        new(
            item.Id,
            item.FileName,
            item.ImportedByUserId,
            item.ImportedAtUtc,
            item.TotalRows,
            item.MatchedRows,
            item.UnmatchedRows,
            item.AmbiguousRows,
            item.MismatchRows,
            item.InvalidRows);

    private static PayoutBankStatementEntryDto ToEntryDto(
        Zadana.Domain.Modules.Wallets.Entities.PayoutBankStatementEntry item) =>
        new(
            item.Id,
            item.ImportId,
            item.RowNumber,
            item.BankReference,
            item.Amount,
            item.TransactionDateUtc,
            item.CurrencyCode,
            item.BeneficiaryMasked,
            item.Status.ToString(),
            item.PayoutId,
            item.MatchedByUserId,
            item.MatchedAtUtc,
            item.ResolutionNote);
}

public sealed record PayoutBankStatementImportListDto(
    IReadOnlyList<PayoutBankStatementImportDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PayoutBankStatementImportDetailDto(
    PayoutBankStatementImportDto Import,
    PayoutBankStatementEntryListDto Entries);

public sealed record PayoutBankStatementImportDto(
    Guid Id,
    string FileName,
    Guid ImportedByUserId,
    DateTime ImportedAtUtc,
    int TotalRows,
    int MatchedRows,
    int UnmatchedRows,
    int AmbiguousRows,
    int MismatchRows,
    int InvalidRows);

public sealed record PayoutBankStatementEntryListDto(
    IReadOnlyList<PayoutBankStatementEntryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PayoutBankStatementEntryDto(
    Guid Id,
    Guid ImportId,
    int RowNumber,
    string BankReference,
    decimal Amount,
    DateTime TransactionDateUtc,
    string CurrencyCode,
    string? BeneficiaryMasked,
    string Status,
    Guid? PayoutId,
    Guid? MatchedByUserId,
    DateTime? MatchedAtUtc,
    string? ResolutionNote);

public sealed record MatchPayoutBankStatementEntryRequest(Guid PayoutId, string? Note = null);

public sealed record IgnorePayoutBankStatementEntryRequest(string? Note = null);
