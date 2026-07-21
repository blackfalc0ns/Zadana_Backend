using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Finances.Controllers;

[ApiController]
[Route("api/admin/payouts")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminPayoutsController(
    IApplicationDbContext context,
    PayoutOrchestrator payoutOrchestrator,
    ISettlementProcessingSettingsService? settlementProcessingSettingsService = null,
    ICurrentUserService? currentUserService = null) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminPayoutListDto>> GetPayouts(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = context.Payouts
            .AsNoTracking()
            .Include(item => item.Settlement)
            .Include(item => item.ManualConfirmation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status.ToString() == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AdminPayoutDto(
                item.Id,
                item.SettlementId,
                item.Settlement.OwnerType.ToString(),
                item.Settlement.OwnerId,
                item.Amount,
                item.Status.ToString(),
                item.ProviderName,
                item.ProviderTransferId,
                item.ProviderSequenceNumber,
                item.TransferReference,
                item.FailureReason,
                item.TriggeredAtUtc,
                item.CompletedAtUtc,
                item.ProcessedByUserId,
                item.ManualConfirmation == null
                    ? null
                    : new AdminManualPayoutConfirmationDto(
                        item.ManualConfirmation.Id,
                        item.ManualConfirmation.TransferReference,
                        item.ManualConfirmation.ProofUrl,
                        item.ManualConfirmation.ConfirmedByUserId,
                        item.ManualConfirmation.ConfirmedAtUtc)))
            .ToListAsync(cancellationToken);

        return Ok(new AdminPayoutListDto(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminPayoutDetailDto>> GetPayout(Guid id, CancellationToken cancellationToken)
    {
        var payout = await context.Payouts
            .AsNoTracking()
            .Include(item => item.Settlement)
            .Include(item => item.Attempts)
            .Include(item => item.ManualConfirmation)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (payout is null)
        {
            return NotFound();
        }

        return Ok(new AdminPayoutDetailDto(
            new AdminPayoutDto(
                payout.Id,
                payout.SettlementId,
                payout.Settlement.OwnerType.ToString(),
                payout.Settlement.OwnerId,
                payout.Amount,
                payout.Status.ToString(),
                payout.ProviderName,
                payout.ProviderTransferId,
                payout.ProviderSequenceNumber,
                payout.TransferReference,
                payout.FailureReason,
                payout.TriggeredAtUtc,
                payout.CompletedAtUtc,
                payout.ProcessedByUserId,
                payout.ManualConfirmation is null
                    ? null
                    : new AdminManualPayoutConfirmationDto(
                        payout.ManualConfirmation.Id,
                        payout.ManualConfirmation.TransferReference,
                        payout.ManualConfirmation.ProofUrl,
                        payout.ManualConfirmation.ConfirmedByUserId,
                        payout.ManualConfirmation.ConfirmedAtUtc)),
            payout.Attempts
                .OrderByDescending(item => item.CreatedAtUtc)
                .Select(item => new AdminPayoutAttemptDto(
                    item.Id,
                    item.AttemptType.ToString(),
                    item.Status.ToString(),
                    item.ProviderName,
                    item.ProviderTransferId,
                    item.TransferReference,
                    item.FailureReason,
                    item.CreatedAtUtc))
                .ToList()));
    }

    [HttpPost("{id:guid}/trigger")]
    public async Task<ActionResult<AdminPayoutDto>> Trigger(Guid id, CancellationToken cancellationToken)
    {
        var payout = await payoutOrchestrator.TriggerAsync(id, cancellationToken: cancellationToken);
        return Ok(await LoadDtoAsync(payout.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<AdminPayoutDto>> Retry(Guid id, CancellationToken cancellationToken)
    {
        var payout = await payoutOrchestrator.TriggerAsync(id, isRetry: true, cancellationToken: cancellationToken);
        return Ok(await LoadDtoAsync(payout.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await payoutOrchestrator.CancelAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/mark-paid")]
    public async Task<ActionResult<AdminPayoutDto>> MarkPaid(
        Guid id,
        [FromBody] AdminConfirmManualPayoutRequest request,
        CancellationToken cancellationToken)
    {
        return await ConfirmManualCoreAsync(id, request, cancellationToken);
    }

    [HttpPost("{id:guid}/confirm-manual")]
    public Task<ActionResult<AdminPayoutDto>> ConfirmManual(
        Guid id,
        [FromBody] AdminConfirmManualPayoutRequest request,
        CancellationToken cancellationToken) =>
        ConfirmManualCoreAsync(id, request, cancellationToken);

    [HttpGet("processing-settings")]
    public async Task<ActionResult<SettlementProcessingSettingsDto>> GetProcessingSettings(CancellationToken cancellationToken)
    {
        var settings = await RequireSettlementProcessingSettingsService().GetAsync(cancellationToken);
        return Ok(ToSettingsDto(settings));
    }

    [HttpPut("processing-settings")]
    public async Task<ActionResult<SettlementProcessingSettingsDto>> UpdateProcessingSettings(
        [FromBody] UpdateSettlementProcessingSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            !Enum.TryParse<SettlementProcessingMode>(request.SettlementProcessingMode, true, out var mode) ||
            !Enum.IsDefined(mode))
        {
            return BadRequest(new { error = "SETTLEMENT_PROCESSING_MODE_INVALID" });
        }

        var actorUserId = currentUserService?.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var settings = await RequireSettlementProcessingSettingsService()
            .SetModeAsync(mode, actorUserId, cancellationToken);

        return Ok(ToSettingsDto(settings));
    }

    [HttpGet("processing-settings/audit")]
    public async Task<ActionResult<SettlementProcessingModeAuditListDto>> GetProcessingSettingsAudit(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = context.SettlementProcessingModeAudits
            .AsNoTracking()
            .OrderByDescending(item => item.ChangedAtUtc);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SettlementProcessingModeAuditDto(
                item.Id,
                item.PreviousMode.ToString(),
                item.NewMode.ToString(),
                item.ChangedByUserId,
                item.ChangedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new SettlementProcessingModeAuditListDto(items, page, pageSize, totalCount));
    }

    private async Task<AdminPayoutDto> LoadDtoAsync(Guid payoutId, CancellationToken cancellationToken)
    {
        return await context.Payouts
            .AsNoTracking()
            .Include(item => item.Settlement)
            .Include(item => item.ManualConfirmation)
            .Where(item => item.Id == payoutId)
            .Select(item => new AdminPayoutDto(
                item.Id,
                item.SettlementId,
                item.Settlement.OwnerType.ToString(),
                item.Settlement.OwnerId,
                item.Amount,
                item.Status.ToString(),
                item.ProviderName,
                item.ProviderTransferId,
                item.ProviderSequenceNumber,
                item.TransferReference,
                item.FailureReason,
                item.TriggeredAtUtc,
                item.CompletedAtUtc,
                item.ProcessedByUserId,
                item.ManualConfirmation == null
                    ? null
                    : new AdminManualPayoutConfirmationDto(
                        item.ManualConfirmation.Id,
                        item.ManualConfirmation.TransferReference,
                        item.ManualConfirmation.ProofUrl,
                        item.ManualConfirmation.ConfirmedByUserId,
                        item.ManualConfirmation.ConfirmedAtUtc)))
            .FirstAsync(cancellationToken);
    }

    private async Task<ActionResult<AdminPayoutDto>> ConfirmManualCoreAsync(
        Guid payoutId,
        AdminConfirmManualPayoutRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TransferReference))
        {
            return BadRequest(new { error = "TRANSFER_REFERENCE_REQUIRED" });
        }

        if (string.IsNullOrWhiteSpace(request.ProofUrl))
        {
            return BadRequest(new { error = "PAYOUT_PROOF_REQUIRED" });
        }

        if (!Uri.TryCreate(request.ProofUrl.Trim(), UriKind.Absolute, out var proofUri) ||
            (proofUri.Scheme != Uri.UriSchemeHttps && proofUri.Scheme != Uri.UriSchemeHttp))
        {
            return BadRequest(new { error = "PAYOUT_PROOF_URL_INVALID" });
        }

        var actorUserId = currentUserService?.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var payout = await payoutOrchestrator.ConfirmManualAsync(
            payoutId,
            request.TransferReference,
            request.ProofUrl,
            actorUserId,
            cancellationToken);

        return Ok(await LoadDtoAsync(payout.Id, cancellationToken));
    }

    private ISettlementProcessingSettingsService RequireSettlementProcessingSettingsService() =>
        settlementProcessingSettingsService
        ?? throw new InvalidOperationException("Settlement processing settings service is not registered.");

    private static SettlementProcessingSettingsDto ToSettingsDto(
        Zadana.Domain.Modules.Wallets.Entities.SettlementProcessingSettings settings) =>
        new(settings.Mode.ToString(), settings.UpdatedByUserId, settings.UpdatedAtUtc);
}

public sealed record AdminPayoutListDto(
    IReadOnlyList<AdminPayoutDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminPayoutDto(
    Guid Id,
    Guid SettlementId,
    string OwnerType,
    Guid OwnerId,
    decimal Amount,
    string Status,
    string ProviderName,
    string? ProviderTransferId,
    string? ProviderSequenceNumber,
    string? TransferReference,
    string? FailureReason,
    DateTime? TriggeredAtUtc,
    DateTime? CompletedAtUtc,
    Guid? ProcessedByUserId,
    AdminManualPayoutConfirmationDto? ManualConfirmation);

public sealed record AdminPayoutDetailDto(
    AdminPayoutDto Payout,
    IReadOnlyList<AdminPayoutAttemptDto> Attempts);

public sealed record AdminPayoutAttemptDto(
    Guid Id,
    string AttemptType,
    string Status,
    string ProviderName,
    string? ProviderTransferId,
    string? TransferReference,
    string? FailureReason,
    DateTime CreatedAtUtc);

public sealed record AdminManualPayoutConfirmationDto(
    Guid Id,
    string TransferReference,
    string ProofUrl,
    Guid ConfirmedByUserId,
    DateTime ConfirmedAtUtc);

public sealed record AdminConfirmManualPayoutRequest(
    string TransferReference,
    string ProofUrl);

public sealed record SettlementProcessingSettingsDto(
    string SettlementProcessingMode,
    Guid? UpdatedByUserId,
    DateTime UpdatedAtUtc);

public sealed record UpdateSettlementProcessingSettingsRequest(string SettlementProcessingMode);

public sealed record SettlementProcessingModeAuditListDto(
    IReadOnlyList<SettlementProcessingModeAuditDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record SettlementProcessingModeAuditDto(
    Guid Id,
    string PreviousMode,
    string NewMode,
    Guid ChangedByUserId,
    DateTime ChangedAtUtc);
