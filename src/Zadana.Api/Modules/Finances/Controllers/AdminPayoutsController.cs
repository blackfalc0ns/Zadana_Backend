using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Authorization;
using Zadana.Api.Modules.Finances.Services;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Finances.Controllers;

[ApiController]
[Route("api/admin/payouts")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminPayoutsController(
    IApplicationDbContext context,
    PayoutOrchestrator payoutOrchestrator,
    PayoutProofAttachmentService payoutProofAttachmentService,
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
            .Include(item => item.ExecutionReservation)
            .Include(item => item.Reversal)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status.ToString() == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var payouts = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = payouts.Select(ToPayoutDto).ToList();

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
            .Include(item => item.ExecutionReservation)
            .Include(item => item.Reversal)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (payout is null)
        {
            return NotFound();
        }

        return Ok(new AdminPayoutDetailDto(
            ToPayoutDto(payout),
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
        await payoutOrchestrator.CancelAsync(id, RequireCurrentUserId(), cancellationToken);
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

    [HttpPost("{id:guid}/manual-claim")]
    public async Task<ActionResult<AdminPayoutDto>> ClaimManual(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actorUserId = RequireCurrentUserId();
        var payout = await payoutOrchestrator.ClaimManualAsync(id, actorUserId, cancellationToken);
        return Ok(await LoadDtoAsync(payout.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/manual-bank-submission")]
    public async Task<ActionResult<AdminPayoutDto>> RecordManualBankSubmission(
        Guid id,
        [FromBody] AdminRecordManualBankSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.BankSubmissionReference))
        {
            return BadRequest(new { error = "BANK_SUBMISSION_REFERENCE_REQUIRED" });
        }

        var payout = await payoutOrchestrator.RecordManualBankSubmissionAsync(
            id,
            request.BankSubmissionReference,
            RequireCurrentUserId(),
            cancellationToken);
        return Ok(await LoadDtoAsync(payout.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/manual-claim/release")]
    public async Task<ActionResult<AdminPayoutDto>> ReleaseManualClaim(
        Guid id,
        [FromBody] AdminReleaseManualClaimRequest? request,
        CancellationToken cancellationToken)
    {
        var payout = await payoutOrchestrator.ReleaseManualClaimAsync(
            id,
            RequireCurrentUserId(),
            request?.Reason,
            cancellationToken);
        return Ok(await LoadDtoAsync(payout.Id, cancellationToken));
    }

    /// <summary>
    /// Uploads immutable, encrypted evidence for a manual bank transfer or a
    /// returned payout. The file never goes through public media storage and
    /// is scoped to this payout before it can be used in a confirmation.
    /// </summary>
    [HttpPost("{id:guid}/proofs")]
    [RequireAccess(PermissionKeys.Admin.FinancesApprove)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<ActionResult<AdminPayoutProofAttachmentDto>> UploadProof(
        Guid id,
        [FromForm] string kind,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PayoutProofKind>(kind, true, out var parsedKind) ||
            !Enum.IsDefined(parsedKind))
        {
            return BadRequest(new { error = "PAYOUT_PROOF_KIND_INVALID" });
        }

        var attachment = await payoutProofAttachmentService.UploadAsync(
            id,
            parsedKind,
            file,
            RequireCurrentUserId(),
            cancellationToken);

        return Ok(ToProofAttachmentDto(attachment));
    }

    /// <summary>
    /// Streams a protected payout proof to a finance approver. It intentionally
    /// returns an attachment response instead of a public or long-lived URL.
    /// </summary>
    [HttpGet("{id:guid}/proofs/{attachmentId:guid}")]
    [RequireAccess(PermissionKeys.Admin.FinancesApprove)]
    public async Task<IActionResult> DownloadProof(
        Guid id,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var proof = await payoutProofAttachmentService.GetForDownloadAsync(
            id,
            attachmentId,
            cancellationToken);

        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        return File(proof.Content, proof.ContentType, proof.FileName);
    }

    [HttpPost("{id:guid}/record-return")]
    public async Task<ActionResult<AdminPayoutDto>> RecordReturn(
        Guid id,
        [FromBody] AdminRecordPayoutReturnRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ReturnReference))
        {
            return BadRequest(new { error = "RETURN_REFERENCE_REQUIRED" });
        }

        if (request.ProofAttachmentId == Guid.Empty)
        {
            return BadRequest(new { error = "PAYOUT_PROOF_REQUIRED" });
        }

        var payout = await payoutOrchestrator.RecordReturnAsync(
            id,
            request.ReturnReference,
            request.ProofAttachmentId,
            RequireCurrentUserId(),
            request.Reason,
            cancellationToken);
        return Ok(await LoadDtoAsync(payout.Id, cancellationToken));
    }

    [HttpGet("processing-settings")]
    public async Task<ActionResult<SettlementProcessingSettingsDto>> GetProcessingSettings(CancellationToken cancellationToken)
    {
        var settings = await RequireSettlementProcessingSettingsService().GetAsync(cancellationToken);
        SetSettingsETag(settings.RowVersion);
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

        if (!TryReadSettingsIfMatch(out var expectedRowVersion))
        {
            return StatusCode(StatusCodes.Status428PreconditionRequired, new
            {
                error = "SETTLEMENT_PROCESSING_SETTINGS_IF_MATCH_REQUIRED"
            });
        }

        IReadOnlyCollection<PayoutScheduleDay>? payoutDays = null;
        if (request.PayoutDays is not null)
        {
            if (request.PayoutDays.Count == 0)
            {
                return BadRequest(new { error = "PAYOUT_DAYS_REQUIRED" });
            }

            var parsedPayoutDays = new List<PayoutScheduleDay>();
            foreach (var value in request.PayoutDays)
            {
                if (!PayoutScheduleDayPolicy.TryParse(value, out var payoutDay))
                {
                    return BadRequest(new { error = "INVALID_PAYOUT_DAY" });
                }

                parsedPayoutDays.Add(payoutDay);
            }

            payoutDays = parsedPayoutDays;
        }

        var actorUserId = currentUserService?.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");
        var settings = await RequireSettlementProcessingSettingsService()
            .UpdateAsync(
                mode,
                payoutDays,
                actorUserId,
                request.RequireManualPayoutDualControl,
                expectedRowVersion,
                cancellationToken);

        SetSettingsETag(settings.RowVersion);
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
        var payout = await context.Payouts
            .AsNoTracking()
            .Include(item => item.Settlement)
            .Include(item => item.ManualConfirmation)
            .Include(item => item.ExecutionReservation)
            .Include(item => item.Reversal)
            .Where(item => item.Id == payoutId)
            .FirstAsync(cancellationToken);
        return ToPayoutDto(payout);
    }

    private static AdminPayoutDto ToPayoutDto(Zadana.Domain.Modules.Wallets.Entities.Payout payout) =>
        new(
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
                    payout.ManualConfirmation.ProofAttachmentId,
                    !string.IsNullOrWhiteSpace(payout.ManualConfirmation.LegacyProofUrl),
                    payout.ManualConfirmation.ConfirmedByUserId,
                    payout.ManualConfirmation.ConfirmedAtUtc),
            ToExecutionReservationDto(payout.ExecutionReservation),
            ToReversalDto(payout.Reversal),
            PayoutDestinationSnapshotCodec.ToMaskedLabel(payout.DestinationSnapshot),
            payout.ScheduledPayoutDay?.ToString());

    private async Task<ActionResult<AdminPayoutDto>> ConfirmManualCoreAsync(
        Guid payoutId,
        AdminConfirmManualPayoutRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TransferReference))
        {
            return BadRequest(new { error = "TRANSFER_REFERENCE_REQUIRED" });
        }

        if (request.ProofAttachmentId == Guid.Empty)
        {
            return BadRequest(new { error = "PAYOUT_PROOF_REQUIRED" });
        }

        var actorUserId = RequireCurrentUserId();
        var payout = await payoutOrchestrator.ConfirmManualAsync(
            payoutId,
            request.TransferReference,
            request.ProofAttachmentId,
            actorUserId,
            cancellationToken);

        return Ok(await LoadDtoAsync(payout.Id, cancellationToken));
    }

    private Guid RequireCurrentUserId() => currentUserService?.UserId
        ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

    private static AdminPayoutExecutionReservationDto? ToExecutionReservationDto(
        Zadana.Domain.Modules.Wallets.Entities.PayoutExecutionReservation? reservation) =>
        reservation is null
            ? null
            : new AdminPayoutExecutionReservationDto(
                reservation.Id,
                reservation.Mode.ToString(),
                reservation.Status.ToString(),
                reservation.ClaimedByUserId,
                reservation.ClaimedAtUtc,
                reservation.SubmittedByUserId,
                reservation.SubmittedAtUtc,
                reservation.SubmissionReference,
                reservation.ReleasedByUserId,
                reservation.ReleasedAtUtc,
                reservation.ReleaseReason);

    private static AdminPayoutReversalDto? ToReversalDto(
        Zadana.Domain.Modules.Wallets.Entities.PayoutReversal? reversal) =>
        reversal is null
            ? null
            : new AdminPayoutReversalDto(
                reversal.Id,
                reversal.ReturnReference,
                reversal.ProofAttachmentId,
                !string.IsNullOrWhiteSpace(reversal.LegacyProofUrl),
                reversal.Reason,
                reversal.ConfirmedByUserId,
                reversal.ConfirmedAtUtc);

    private static AdminPayoutProofAttachmentDto ToProofAttachmentDto(
        Zadana.Domain.Modules.Wallets.Entities.PayoutProofAttachment attachment) =>
        new(
            attachment.Id,
            attachment.PayoutId,
            attachment.Kind.ToString(),
            attachment.FileName,
            attachment.ContentType,
            attachment.ContentLength,
            attachment.Sha256,
            attachment.UploadedByUserId,
            attachment.UploadedAtUtc,
            attachment.IsFinalized,
            attachment.FinalizedByUserId,
            attachment.FinalizedAtUtc);

    private ISettlementProcessingSettingsService RequireSettlementProcessingSettingsService() =>
        settlementProcessingSettingsService
        ?? throw new InvalidOperationException("Settlement processing settings service is not registered.");

    private static SettlementProcessingSettingsDto ToSettingsDto(
        Zadana.Domain.Modules.Wallets.Entities.SettlementProcessingSettings settings) =>
        new(
            settings.Mode.ToString(),
            settings.GetPayoutDays().Select(day => day.ToString()).ToArray(),
            settings.RequireManualPayoutDualControl,
            settings.UpdatedByUserId,
            settings.UpdatedAtUtc,
            Convert.ToBase64String(settings.RowVersion));

    private bool TryReadSettingsIfMatch(out byte[] rowVersion)
    {
        rowVersion = [];
        var value = Request.Headers.IfMatch.ToString().Trim();
        if (string.IsNullOrWhiteSpace(value) || value == "*" || value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        try
        {
            rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void SetSettingsETag(byte[] rowVersion)
    {
        if (rowVersion.Length > 0)
        {
            Response.Headers["ETag"] = $"\"{Convert.ToBase64String(rowVersion)}\"";
        }
    }
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
    AdminManualPayoutConfirmationDto? ManualConfirmation,
    AdminPayoutExecutionReservationDto? ExecutionReservation,
    AdminPayoutReversalDto? Reversal,
    string? DestinationMaskedLabel,
    string? ScheduledPayoutDay);

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
    Guid? ProofAttachmentId,
    bool HasLegacyProof,
    Guid ConfirmedByUserId,
    DateTime ConfirmedAtUtc);

public sealed record AdminPayoutExecutionReservationDto(
    Guid Id,
    string Mode,
    string Status,
    Guid? ClaimedByUserId,
    DateTime ClaimedAtUtc,
    Guid? SubmittedByUserId,
    DateTime? SubmittedAtUtc,
    string? SubmissionReference,
    Guid? ReleasedByUserId,
    DateTime? ReleasedAtUtc,
    string? ReleaseReason);

public sealed record AdminPayoutReversalDto(
    Guid Id,
    string ReturnReference,
    Guid? ProofAttachmentId,
    bool HasLegacyProof,
    string? Reason,
    Guid ConfirmedByUserId,
    DateTime ConfirmedAtUtc);

public sealed record AdminPayoutProofAttachmentDto(
    Guid Id,
    Guid PayoutId,
    string Kind,
    string FileName,
    string ContentType,
    long ContentLength,
    string Sha256,
    Guid UploadedByUserId,
    DateTime UploadedAtUtc,
    bool IsFinalized,
    Guid? FinalizedByUserId,
    DateTime? FinalizedAtUtc);

public sealed record AdminConfirmManualPayoutRequest(
    string TransferReference,
    Guid ProofAttachmentId);

public sealed record AdminRecordManualBankSubmissionRequest(string BankSubmissionReference);

public sealed record AdminReleaseManualClaimRequest(string? Reason = null);

public sealed record AdminRecordPayoutReturnRequest(
    string ReturnReference,
    Guid ProofAttachmentId,
    string? Reason = null);

public sealed record SettlementProcessingSettingsDto(
    string SettlementProcessingMode,
    IReadOnlyList<string> PayoutDays,
    bool RequireManualPayoutDualControl,
    Guid? UpdatedByUserId,
    DateTime UpdatedAtUtc,
    string RowVersion);

/// <summary>
/// <paramref name="PayoutDays"/> is optional for backwards-compatible mode
/// updates. When supplied it must contain at least one valid weekday.
/// </summary>
public sealed record UpdateSettlementProcessingSettingsRequest(
    string SettlementProcessingMode,
    IReadOnlyList<string>? PayoutDays = null,
    bool? RequireManualPayoutDualControl = null);

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
