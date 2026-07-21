using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Delivery.Support;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;
using Zadana.SharedKernel.Serialization;

namespace Zadana.Application.Modules.Finances.Services;

public sealed class PayoutOrchestrator
{
    // FinancialEvents.IdempotencyKey is capped at 160 characters. Payout
    // references can legitimately be up to 200 characters, so retain the
    // legacy readable key when it fits and use a deterministic digest only
    // for the oversized case.
    private const int FinancialEventIdempotencyKeyMaxLength = 160;

    private readonly IApplicationDbContext _context;
    private readonly IEnumerable<IPayoutGateway> _payoutGateways;
    private readonly FinancialEventPostingService _postingService;
    private readonly WalletProjectionUpdater _walletProjectionUpdater;
    private readonly VendorPayoutWalletService _vendorPayoutWalletService;
    private readonly FinancialSettingsOptions _settings;
    private readonly IAdminAlertService _adminAlertService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;
    private readonly ISettlementProcessingSettingsService? _settlementProcessingSettingsService;

    public PayoutOrchestrator(
        IApplicationDbContext context,
        IEnumerable<IPayoutGateway> payoutGateways,
        FinancialEventPostingService postingService,
        WalletProjectionUpdater walletProjectionUpdater,
        VendorPayoutWalletService vendorPayoutWalletService,
        IOptions<FinancialSettingsOptions> settings,
        IAdminAlertService adminAlertService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService,
        ISettlementProcessingSettingsService? settlementProcessingSettingsService = null)
    {
        _context = context;
        _payoutGateways = payoutGateways;
        _postingService = postingService;
        _walletProjectionUpdater = walletProjectionUpdater;
        _vendorPayoutWalletService = vendorPayoutWalletService;
        _settings = settings.Value;
        _adminAlertService = adminAlertService;
        _notificationService = notificationService;
        _oneSignalPushService = oneSignalPushService;
        _settlementProcessingSettingsService = settlementProcessingSettingsService;
    }

    public bool HasEnabledGateway => GetEnabledGateway() is not null;

    /// <summary>
    /// Returns whether new gateway submissions are enabled. Existing in-flight
    /// payouts are deliberately not affected; their status can still be read
    /// through <see cref="RefreshStatusAsync"/> in Manual mode.
    /// </summary>
    public Task<bool> IsAutomaticProcessingEnabledAsync(CancellationToken cancellationToken = default) =>
        _settlementProcessingSettingsService?.IsAutomaticAsync(cancellationToken)
        ?? Task.FromResult(true);

    public async Task<Payout> TriggerAsync(Guid payoutId, Guid? processedByUserId = null, bool isRetry = false, CancellationToken cancellationToken = default)
    {
        var payout = await LoadPayoutAsync(payoutId, cancellationToken);

        if (payout.Status is PayoutStatus.Paid or PayoutStatus.Reversed ||
            (payout.Status == PayoutStatus.Cancelled && !isRetry))
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_CLOSED", "Closed payouts cannot be triggered.");
        }

        // A manual claim is a durable ownership boundary. It remains in force
        // even if finance switches the global mode back to Automatic, so the
        // worker cannot create a second gateway transfer for a bank transfer
        // which an administrator has already prepared.
        if (payout.ExecutionReservation?.IsManualActive == true)
        {
            return payout;
        }

        // An automatic reservation is persisted before the external provider
        // call. A retry must reconcile an existing submission instead of
        // posting a duplicate command with the same payout amount.
        if (payout.ExecutionReservation?.IsAutomaticActive == true)
        {
            return payout;
        }

        if (!isRetry && payout.Status is PayoutStatus.Queued or PayoutStatus.Processing)
        {
            return payout;
        }

        // Do not create a new provider transfer or retry an unknown one while
        // finance has selected Manual mode. The payout remains pending for the
        // administrator to complete after the bank transfer is made outside
        // the platform.
        if (!await IsAutomaticProcessingEnabledAsync(cancellationToken))
        {
            return payout;
        }

        EnsureSettlementCanBeTriggered(payout);

        // Automatic submissions and retries are schedule-bound as well. The
        // status-sync worker may examine a pending payout every few minutes,
        // so this intentionally returns it unchanged until the owner is due
        // instead of raising an error or creating an off-cycle gateway attempt.
        if (!await IsAutomaticPayoutDueTodayAsync(payout, cancellationToken))
        {
            return payout;
        }

        var gateway = GetEnabledGateway();

        // Do not manufacture a fake "Manual" processing payout when no
        // gateway is configured. It must stay pending until finance either
        // enables a gateway or explicitly claims it through the manual flow.
        if (gateway is null)
        {
            return payout;
        }

        CreatePayoutCommand? command = null;
        var providerSubmitAttempted = false;

        try
        {
            command = await BuildGatewayCommandAsync(payout, cancellationToken);
            ValidateGatewayCommand(command);

            if (!await ReserveAutomaticSubmissionAsync(
                    payout,
                    gateway.ProviderName,
                    command.SequenceNumber,
                    isRetry,
                    cancellationToken))
            {
                return await LoadPayoutAsync(payout.Id, cancellationToken);
            }

            // The reservation is deliberately persisted before the external
            // POST. A process crash after provider acceptance leaves a durable
            // submitted reservation for reconciliation, never a retryable
            // second payout.
            providerSubmitAttempted = true;
            var result = await gateway.CreatePayoutAsync(command, cancellationToken);

            _context.PayoutAttempts.Add(new PayoutAttempt(
                payout.Id,
                isRetry ? PayoutAttemptType.Retry : PayoutAttemptType.Trigger,
                MapProviderStatus(result.ProviderStatus),
                providerName: result.ProviderName,
                providerTransferId: result.ProviderTransferId,
                transferReference: payout.TransferReference,
                failureReason: result.FailureMessage,
                rawPayload: result.RawResponse));

            if (IsPaid(result.ProviderStatus))
            {
                await MarkPaidCoreAsync(
                    payout,
                    result.ProviderSequenceNumber ?? command.SequenceNumber ?? payout.ProviderSequenceNumber ?? payout.Id.ToString("N"),
                    result.ProviderTransferId,
                    result.ProviderName,
                    result.ProviderSequenceNumber ?? command.SequenceNumber,
                    cancellationToken);
            }
            else if (result.IsTransient || IsPending(result.ProviderStatus) || IsUnknown(result.ProviderStatus))
            {
                ApplyProviderPendingStatus(
                    payout,
                    IsUnknown(result.ProviderStatus) ? "processing" : result.ProviderStatus,
                    result.ProviderTransferId,
                    result.ProviderName,
                    result.ProviderSequenceNumber ?? command.SequenceNumber);
                await _context.SaveChangesAsync(cancellationToken);

                if (result.IsTransient || IsUnknown(result.ProviderStatus))
                {
                    await SendPayoutRequiresReviewAlertAsync(
                        payout,
                        result.FailureMessage ?? "Moyasar returned an unknown payout state.",
                        cancellationToken);
                }
            }
            else
            {
                await MarkFailedCoreAsync(
                    payout,
                    result.FailureMessage ?? $"Provider returned status '{result.ProviderStatus}'.",
                    result.ProviderTransferId,
                    result.ProviderName,
                    result.ProviderSequenceNumber ?? command.SequenceNumber,
                    cancellationToken);
                await SendPayoutFailedAlertAsync(payout, result.FailureMessage, cancellationToken);
            }

            return payout;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsTransientIntegrationException(ex))
        {
            await MarkPayoutUnknownAsync(
                payout,
                gateway.ProviderName,
                command?.SequenceNumber ?? payout.ProviderSequenceNumber ?? BuildSequenceNumber(payout.Id),
                ex.Message,
                isRetry,
                cancellationToken);
            await SendPayoutRequiresReviewAlertAsync(payout, ex.Message, cancellationToken);
            return payout;
        }
        catch (Exception ex)
        {
            if (providerSubmitAttempted)
            {
                await MarkPayoutUnknownAsync(
                    payout,
                    gateway.ProviderName,
                    command?.SequenceNumber ?? payout.ProviderSequenceNumber ?? BuildSequenceNumber(payout.Id),
                    ex.Message,
                    isRetry,
                    cancellationToken);
                await SendPayoutRequiresReviewAlertAsync(
                    payout,
                    $"Provider submit was attempted, but local finalization failed: {ex.Message}",
                    cancellationToken);
                return payout;
            }

            await MarkFailedCoreAsync(
                payout,
                ex.Message,
                payout.ProviderTransferId,
                payout.ProviderName,
                payout.ProviderSequenceNumber,
                cancellationToken);
            await SendPayoutIntegrationFailureAlertAsync(payout, ex, cancellationToken);
            throw;
        }
    }

    public async Task<Payout> RefreshStatusAsync(Guid payoutId, CancellationToken cancellationToken = default)
    {
        var payout = await LoadPayoutAsync(payoutId, cancellationToken);

        if (payout.Status is PayoutStatus.Paid or PayoutStatus.Reversed or PayoutStatus.Cancelled)
        {
            return payout;
        }

        if (string.IsNullOrWhiteSpace(payout.ProviderTransferId) ||
            string.Equals(payout.ProviderName, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            return payout;
        }

        var gateway = GetEnabledGateway(payout.ProviderName) ?? GetEnabledGateway();
        if (gateway is null)
        {
            return payout;
        }

        var details = await gateway.FetchPayoutAsync(payout.ProviderTransferId, cancellationToken);

        _context.PayoutAttempts.Add(new PayoutAttempt(
            payout.Id,
            PayoutAttemptType.ProviderCallback,
            MapProviderStatus(details.ProviderStatus),
            providerName: details.ProviderName,
            providerTransferId: details.ProviderTransferId,
            transferReference: payout.TransferReference,
            failureReason: details.FailureMessage,
            rawPayload: details.RawResponse));

        if (IsPaid(details.ProviderStatus))
        {
            await MarkPaidCoreAsync(
                payout,
                details.ProviderSequenceNumber ?? details.ProviderTransferId,
                details.ProviderTransferId,
                details.ProviderName,
                details.ProviderSequenceNumber,
                cancellationToken);
        }
        else if (IsPending(details.ProviderStatus) || IsUnknown(details.ProviderStatus))
        {
            ApplyProviderPendingStatus(
                payout,
                IsUnknown(details.ProviderStatus) ? "processing" : details.ProviderStatus,
                details.ProviderTransferId,
                details.ProviderName,
                details.ProviderSequenceNumber);
            await _context.SaveChangesAsync(cancellationToken);

            if (IsUnknown(details.ProviderStatus))
            {
                await SendPayoutRequiresReviewAlertAsync(
                    payout,
                    details.FailureMessage ?? "Moyasar returned an unknown payout state.",
                    cancellationToken);
            }
        }
        else
        {
            await MarkFailedCoreAsync(
                payout,
                details.FailureMessage ?? $"Provider returned status '{details.ProviderStatus}'.",
                details.ProviderTransferId,
                details.ProviderName,
                details.ProviderSequenceNumber,
                cancellationToken);
            await SendPayoutFailedAlertAsync(payout, details.FailureMessage, cancellationToken);
        }

        return payout;
    }

    public async Task<Payout?> ApplyProviderStatusAsync(
        PayoutGatewayDetails details,
        CancellationToken cancellationToken = default)
    {
        var providerTransferId = string.IsNullOrWhiteSpace(details.ProviderTransferId)
            ? null
            : details.ProviderTransferId.Trim();
        var providerSequenceNumber = string.IsNullOrWhiteSpace(details.ProviderSequenceNumber)
            ? null
            : details.ProviderSequenceNumber.Trim();

        if (providerTransferId is null && providerSequenceNumber is null)
        {
            throw new BusinessRuleException("PAYOUT_PROVIDER_REFERENCE_REQUIRED", "Provider payout id or sequence number is required.");
        }

        var payout = await _context.Payouts
            .Include(item => item.Settlement)
            .Include(item => item.VendorBankAccount)
            .Include(item => item.ExecutionReservation)
            .FirstOrDefaultAsync(
                item =>
                    (providerTransferId != null && item.ProviderTransferId == providerTransferId) ||
                    (providerSequenceNumber != null && item.ProviderSequenceNumber == providerSequenceNumber),
                cancellationToken);

        if (payout is null)
        {
            return null;
        }

        if (payout.Status is PayoutStatus.Paid or PayoutStatus.Reversed or PayoutStatus.Cancelled)
        {
            return payout;
        }

        _context.PayoutAttempts.Add(new PayoutAttempt(
            payout.Id,
            PayoutAttemptType.ProviderCallback,
            MapProviderStatus(details.ProviderStatus),
            providerName: details.ProviderName,
            providerTransferId: providerTransferId,
            transferReference: payout.TransferReference,
            failureReason: details.FailureMessage,
            rawPayload: details.RawResponse));

        if (IsPaid(details.ProviderStatus))
        {
            await MarkPaidCoreAsync(
                payout,
                providerSequenceNumber ?? providerTransferId ?? payout.Id.ToString("N"),
                providerTransferId,
                details.ProviderName,
                providerSequenceNumber,
                cancellationToken);
        }
        else if (IsPending(details.ProviderStatus) || IsUnknown(details.ProviderStatus))
        {
            ApplyProviderPendingStatus(
                payout,
                IsUnknown(details.ProviderStatus) ? "processing" : details.ProviderStatus,
                providerTransferId,
                details.ProviderName,
                providerSequenceNumber);
            await _context.SaveChangesAsync(cancellationToken);

            if (IsUnknown(details.ProviderStatus))
            {
                await SendPayoutRequiresReviewAlertAsync(
                    payout,
                    details.FailureMessage ?? "Moyasar webhook returned an unknown payout state.",
                    cancellationToken);
            }
        }
        else
        {
            await MarkFailedCoreAsync(
                payout,
                details.FailureMessage ?? $"Provider returned status '{details.ProviderStatus}'.",
                providerTransferId,
                details.ProviderName,
                providerSequenceNumber,
                cancellationToken);
            await SendPayoutFailedAlertAsync(payout, details.FailureMessage, cancellationToken);
        }

        return payout;
    }

    [Obsolete("Direct payout completion is disabled. Use the manual claim, bank submission, and confirmation workflow.")]
    public Task<Payout> MarkPaidAsync(
        Guid payoutId,
        string transferReference,
        string? providerTransferId = null,
        CancellationToken cancellationToken = default)
    {
        throw new BusinessRuleException(
            "PAYOUT_DIRECT_COMPLETION_DISABLED",
            "Direct payout completion is disabled. Record a manual bank submission and confirm it with an approved proof, or reconcile the provider callback.");
    }

    /// <summary>
    /// Claims a payout for the manual bank workflow. Claiming is the required
    /// first step before an administrator leaves the platform to create the
    /// transfer in the bank portal.
    /// </summary>
    public async Task<Payout> ClaimManualAsync(
        Guid payoutId,
        Guid claimedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (claimedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CLAIMING_USER_REQUIRED", "The administrator claiming a manual payout is required.");
        }

        if (await IsAutomaticProcessingEnabledAsync(cancellationToken))
        {
            throw new BusinessRuleException(
                "SETTLEMENT_PROCESSING_NOT_MANUAL",
                "A payout can only be claimed for manual transfer while settlement processing mode is Manual.");
        }

        var payout = await LoadPayoutAsync(payoutId, cancellationToken);
        if (payout.Status is PayoutStatus.Paid or PayoutStatus.Reversed or PayoutStatus.Cancelled)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_CLOSED", "Closed payouts cannot be claimed for manual transfer.");
        }

        EnsureSettlementCanBeTriggered(payout);
        EnsureManualClaimIsSafe(payout);

        var reservation = payout.ExecutionReservation;
        if (reservation?.IsActive == true)
        {
            if (reservation.Mode == PayoutExecutionMode.Manual &&
                reservation.ClaimedByUserId == claimedByUserId &&
                reservation.Status == PayoutExecutionReservationStatus.Claimed)
            {
                return payout;
            }

            throw new BusinessRuleException(
                "PAYOUT_ALREADY_RESERVED",
                "This payout is already reserved for execution and cannot be claimed again.");
        }

        if (reservation is null)
        {
            reservation = new PayoutExecutionReservation(
                payout.Id,
                PayoutExecutionMode.Manual,
                claimedByUserId);
            _context.PayoutExecutionReservations.Add(reservation);
        }
        else
        {
            reservation.ReclaimManual(claimedByUserId);
        }

        _context.PayoutAttempts.Add(new PayoutAttempt(
            payout.Id,
            PayoutAttemptType.ManualClaim,
            payout.Status,
            providerName: "Manual"));

        await SaveReservationChangesAsync(payout.Id, cancellationToken);
        return payout;
    }

    /// <summary>
    /// Records that the claimed payout was submitted in the external bank
    /// portal. It intentionally happens before confirmation, making a
    /// submitted transfer non-cancellable and non-retryable.
    /// </summary>
    public async Task<Payout> RecordManualBankSubmissionAsync(
        Guid payoutId,
        string bankSubmissionReference,
        Guid submittedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (submittedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_SUBMITTING_USER_REQUIRED", "The administrator submitting the manual bank transfer is required.");
        }

        if (string.IsNullOrWhiteSpace(bankSubmissionReference))
        {
            throw new BusinessRuleException("BANK_SUBMISSION_REFERENCE_REQUIRED", "Bank submission reference is required.");
        }

        var payout = await LoadPayoutAsync(payoutId, cancellationToken);
        if (payout.Status is PayoutStatus.Paid or PayoutStatus.Reversed or PayoutStatus.Cancelled)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_CLOSED", "Closed payouts cannot be submitted to the bank.");
        }

        EnsureSettlementCanBeTriggered(payout);
        await EnsureManualConfirmationIsDueTodayAsync(payout, cancellationToken);

        var reservation = payout.ExecutionReservation;
        if (reservation is null || reservation.Mode != PayoutExecutionMode.Manual)
        {
            throw new BusinessRuleException("PAYOUT_MANUAL_CLAIM_REQUIRED", "Claim the payout before recording a manual bank submission.");
        }

        if (reservation.Status == PayoutExecutionReservationStatus.Submitted)
        {
            if (reservation.SubmittedByUserId == submittedByUserId &&
                string.Equals(reservation.SubmissionReference, bankSubmissionReference.Trim(), StringComparison.Ordinal))
            {
                return payout;
            }

            throw new BusinessRuleException("PAYOUT_ALREADY_SUBMITTED", "This payout has already been submitted to the bank and must be confirmed or reconciled.");
        }

        reservation.MarkSubmitted(submittedByUserId, bankSubmissionReference);
        payout.MarkAsProcessing(providerName: "Manual");
        payout.Settlement.MarkAsProcessing();
        await MarkLinkedDriverWithdrawalProcessingAsync(payout.Id, cancellationToken);
        _context.PayoutAttempts.Add(new PayoutAttempt(
            payout.Id,
            PayoutAttemptType.ManualBankSubmission,
            PayoutStatus.Processing,
            providerName: "Manual",
            transferReference: bankSubmissionReference.Trim()));

        await SaveReservationChangesAsync(payout.Id, cancellationToken);
        return payout;
    }

    /// <summary>
    /// Releases a claimed payout only before a transfer is submitted. Submitted
    /// transfers must use reconciliation or a recorded return/reversal instead.
    /// </summary>
    public async Task<Payout> ReleaseManualClaimAsync(
        Guid payoutId,
        Guid releasedByUserId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (releasedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CLAIMING_USER_REQUIRED", "The administrator releasing the manual payout claim is required.");
        }

        var payout = await LoadPayoutAsync(payoutId, cancellationToken);
        var reservation = payout.ExecutionReservation;
        if (reservation is null || reservation.Mode != PayoutExecutionMode.Manual)
        {
            throw new BusinessRuleException("PAYOUT_MANUAL_CLAIM_REQUIRED", "This payout does not have a manual claim to release.");
        }

        if (reservation.Status == PayoutExecutionReservationStatus.Released)
        {
            return payout;
        }

        reservation.Release(releasedByUserId, reason ?? "Manual payout claim released before bank submission.");
        _context.PayoutAttempts.Add(new PayoutAttempt(
            payout.Id,
            PayoutAttemptType.ManualClaimRelease,
            payout.Status,
            providerName: "Manual",
            failureReason: reason));

        await SaveReservationChangesAsync(payout.Id, cancellationToken);
        return payout;
    }

    public async Task<Payout> ConfirmManualAsync(
        Guid payoutId,
        string transferReference,
        Guid proofAttachmentId,
        Guid confirmedByUserId,
        CancellationToken cancellationToken = default)
    {
        var payout = await LoadPayoutAsync(payoutId, cancellationToken);

        if (payout.Status == PayoutStatus.Paid)
        {
            if (payout.ManualConfirmation is not null)
            {
                return payout;
            }

            throw new BusinessRuleException("PAYOUT_ALREADY_PAID", "This payout was already completed outside the manual confirmation workflow.");
        }

        if (string.IsNullOrWhiteSpace(transferReference))
        {
            throw new BusinessRuleException("TRANSFER_REFERENCE_REQUIRED", "Transfer reference is required for manual payout confirmation.");
        }

        if (proofAttachmentId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_PROOF_REQUIRED", "Transfer proof is required for manual payout confirmation.");
        }

        if (confirmedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CONFIRMING_USER_REQUIRED", "The confirming administrator is required.");
        }

        EnsureSettlementCanBeTriggered(payout);
        EnsureManualConfirmationIsSafe(payout);
        await EnsureManualConfirmationIsDueTodayAsync(payout, cancellationToken);

        var reservation = payout.ExecutionReservation;
        if (reservation is null || reservation.Mode != PayoutExecutionMode.Manual)
        {
            throw new BusinessRuleException("PAYOUT_MANUAL_CLAIM_REQUIRED", "Claim and submit the payout before confirming the manual bank transfer.");
        }

        if (reservation.Status != PayoutExecutionReservationStatus.Submitted)
        {
            throw new BusinessRuleException("PAYOUT_RESERVATION_NOT_SUBMITTED", "Record the external bank submission before confirming the payout.");
        }

        if (await RequiresManualPayoutDualControlAsync(cancellationToken) &&
            reservation.SubmittedByUserId == confirmedByUserId)
        {
            throw new BusinessRuleException(
                "PAYOUT_DUAL_CONTROL_REQUIRED",
                "A different finance approver must confirm a manually submitted payout.");
        }

        var proofAttachment = await RequireFinalizableProofAttachmentAsync(
            payout.Id,
            proofAttachmentId,
            PayoutProofKind.ManualTransfer,
            cancellationToken);

        _context.PayoutManualConfirmations.Add(new PayoutManualConfirmation(
            payout.Id,
            transferReference,
            proofAttachmentId,
            confirmedByUserId));
        proofAttachment.FinalizeForUse(confirmedByUserId);
        reservation.Confirm(confirmedByUserId);

        await MarkManualPaidCoreAsync(
            payout,
            transferReference.Trim(),
            confirmedByUserId,
            cancellationToken);

        return payout;
    }

    public Task CancelAsync(Guid payoutId, CancellationToken cancellationToken = default) =>
        CancelAsync(payoutId, null, cancellationToken);

    public async Task CancelAsync(
        Guid payoutId,
        Guid? cancelledByUserId,
        CancellationToken cancellationToken = default)
    {
        var payout = await LoadPayoutAsync(payoutId, cancellationToken);

        if (payout.Status is PayoutStatus.Paid or PayoutStatus.Reversed)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_CLOSED", "Closed payouts cannot be cancelled.");
        }

        if (payout.Status == PayoutStatus.Cancelled)
        {
            return;
        }

        var reservation = payout.ExecutionReservation;
        if (reservation?.IsAutomaticActive == true ||
            reservation?.Status == PayoutExecutionReservationStatus.Submitted)
        {
            throw new BusinessRuleException(
                "PAYOUT_RECONCILIATION_REQUIRED",
                "A submitted payout cannot be cancelled. Reconcile the execution channel or record a return first.");
        }

        if (payout.Status is PayoutStatus.Queued or PayoutStatus.Processing)
        {
            throw new BusinessRuleException(
                "PAYOUT_IN_FLIGHT_CANNOT_CANCEL",
                "An in-flight payout must be reconciled instead of cancelled.");
        }

        await ExecuteInTransactionAsync(async () =>
        {
            if (reservation?.IsManualActive == true)
            {
                reservation.ReleaseForCancellation(cancelledByUserId, "Manual payout claim cancelled before bank submission.");
            }

            payout.Cancel();
            payout.Settlement.Hold();
            await ReleaseVendorHoldIfApplicableAsync(payout, "Payout cancelled before execution.", cancellationToken);
            await CancelLinkedDriverWithdrawalAsync(payout.Id, "Payout cancelled.", cancellationToken);
            _context.PayoutAttempts.Add(new PayoutAttempt(payout.Id, PayoutAttemptType.Cancel, PayoutStatus.Cancelled));
            await _context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Records a verified return of funds for a previously paid payout. This
    /// never edits the original payment evidence or silently reopens it: it
    /// creates an immutable return record and posts the accounting reversal.
    /// Any corrected payout must subsequently be prepared as a new finance
    /// operation after review.
    /// </summary>
    public async Task<Payout> RecordReturnAsync(
        Guid payoutId,
        string returnReference,
        Guid proofAttachmentId,
        Guid confirmedByUserId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(returnReference))
        {
            throw new BusinessRuleException("RETURN_REFERENCE_REQUIRED", "Bank return reference is required.");
        }

        if (proofAttachmentId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_PROOF_REQUIRED", "Return proof is required.");
        }

        if (confirmedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CONFIRMING_USER_REQUIRED", "The confirming administrator is required.");
        }

        var payout = await LoadPayoutAsync(payoutId, cancellationToken);
        if (payout.Status == PayoutStatus.Reversed)
        {
            if (payout.Reversal is not null)
            {
                return payout;
            }

            throw new BusinessRuleException("PAYOUT_REVERSAL_RECONCILIATION_REQUIRED", "This payout is marked reversed without a return confirmation and needs reconciliation.");
        }

        if (payout.Status != PayoutStatus.Paid)
        {
            throw new BusinessRuleException("PAYOUT_REVERSAL_INVALID_STATUS", "Only a paid payout can be marked as returned.");
        }

        if (payout.Reversal is not null)
        {
            return payout;
        }

        var proofAttachment = await RequireFinalizableProofAttachmentAsync(
            payout.Id,
            proofAttachmentId,
            PayoutProofKind.ReturnedFunds,
            cancellationToken);

        _context.PayoutReversals.Add(new PayoutReversal(
            payout.Id,
            returnReference,
            proofAttachmentId,
            confirmedByUserId,
            reason));
        proofAttachment.FinalizeForUse(confirmedByUserId);

        await ExecuteInTransactionAsync(async () =>
        {
            payout.MarkAsReversed();
            payout.Settlement.MarkReversed();
            _context.PayoutAttempts.Add(new PayoutAttempt(
                payout.Id,
                PayoutAttemptType.Reversal,
                PayoutStatus.Reversed,
                providerName: "Manual",
                transferReference: returnReference.Trim(),
                failureReason: reason));

            await PostPayoutReversedAsync(payout, returnReference.Trim(), cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return payout;
    }

    private async Task<Payout> LoadPayoutAsync(Guid payoutId, CancellationToken cancellationToken)
    {
        return await _context.Payouts
            .Include(item => item.Settlement)
            .Include(item => item.VendorBankAccount)
            .Include(item => item.ManualConfirmation)
            .Include(item => item.ExecutionReservation)
            .Include(item => item.Reversal)
            .FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken)
            ?? throw new NotFoundException("Payout", payoutId);
    }

    private async Task<PayoutProofAttachment> RequireFinalizableProofAttachmentAsync(
        Guid payoutId,
        Guid proofAttachmentId,
        PayoutProofKind requiredKind,
        CancellationToken cancellationToken)
    {
        var attachment = await _context.PayoutProofAttachments
            .FirstOrDefaultAsync(item => item.Id == proofAttachmentId, cancellationToken)
            ?? throw new BusinessRuleException(
                "PAYOUT_PROOF_NOT_FOUND",
                "The selected payout proof attachment was not found.");

        if (attachment.PayoutId != payoutId)
        {
            throw new BusinessRuleException(
                "PAYOUT_PROOF_PAYOUT_MISMATCH",
                "The selected proof attachment belongs to a different payout.");
        }

        if (attachment.Kind != requiredKind)
        {
            throw new BusinessRuleException(
                "PAYOUT_PROOF_KIND_MISMATCH",
                "The selected proof attachment cannot be used for this payout action.");
        }

        if (attachment.IsFinalized)
        {
            throw new BusinessRuleException(
                "PAYOUT_PROOF_ALREADY_FINALIZED",
                "The selected proof attachment has already been finalized.");
        }

        return attachment;
    }

    private async Task<CreatePayoutCommand> BuildGatewayCommandAsync(Payout payout, CancellationToken cancellationToken)
    {
        return payout.Settlement.OwnerType switch
        {
            SettlementOwnerType.Vendor => await BuildVendorGatewayCommandAsync(payout, cancellationToken),
            SettlementOwnerType.Driver => await BuildDriverGatewayCommandAsync(payout, cancellationToken),
            _ => throw new BusinessRuleException("UNSUPPORTED_PAYOUT_OWNER", "Unsupported payout owner.")
        };
    }

    private async Task<CreatePayoutCommand> BuildVendorGatewayCommandAsync(Payout payout, CancellationToken cancellationToken)
    {
        var destination = PayoutDestinationSnapshotCodec.ParseRequired(payout);
        if (destination.DestinationType != PayoutDestinationType.VendorBankAccount)
        {
            throw new BusinessRuleException(
                "PAYOUT_DESTINATION_TYPE_INVALID",
                "Vendor payout has an invalid recipient destination snapshot.");
        }

        var vendor = await _context.Vendors
            .AsNoTracking()
            .Where(item => item.Id == payout.Settlement.OwnerId)
            .Select(item => new { item.ContactPhone, item.City })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Vendor", payout.Settlement.OwnerId);

        return BuildCommand(
            payout,
            destination.AccountHolderName,
            destination.AccountIdentifier,
            destination.ProviderOrBankName,
            vendor.ContactPhone,
            vendor.City,
            purpose: null,
            metadata: new Dictionary<string, string>
            {
                ["payout_id"] = payout.Id.ToString(),
                ["settlement_id"] = payout.SettlementId.ToString(),
                ["vendor_id"] = payout.Settlement.OwnerId.ToString()
            });
    }

    private async Task<CreatePayoutCommand> BuildDriverGatewayCommandAsync(Payout payout, CancellationToken cancellationToken)
    {
        var destination = PayoutDestinationSnapshotCodec.ParseRequired(payout);
        if (destination.DestinationType != PayoutDestinationType.DriverPayoutMethod)
        {
            throw new BusinessRuleException(
                "PAYOUT_DESTINATION_TYPE_INVALID",
                "Driver payout has an invalid recipient destination snapshot.");
        }

        var withdrawal = await _context.DriverWithdrawalRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.PayoutId == payout.Id, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_WITHDRAWAL_REQUIRED", "Driver payout must be linked to a withdrawal request.");

        if (!string.Equals(
                destination.MethodType,
                DriverPayoutMethodType.BankAccount.ToString(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("DRIVER_BANK_ACCOUNT_REQUIRED", "Only bank account withdrawal methods can be paid through Moyasar payouts.");
        }

        if (destination.SourceId != withdrawal.DriverPayoutMethodId)
        {
            throw new BusinessRuleException(
                "PAYOUT_DESTINATION_SNAPSHOT_MISMATCH",
                "Driver payout recipient snapshot does not match the prepared withdrawal method.");
        }

        var driver = await _context.Drivers
            .AsNoTracking()
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == withdrawal.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", withdrawal.DriverId);

        return BuildCommand(
            payout,
            destination.AccountHolderName,
            destination.AccountIdentifier,
            destination.ProviderOrBankName,
            driver.User.PhoneNumber,
            driver.City,
            purpose: null,
            metadata: new Dictionary<string, string>
            {
                ["payout_id"] = payout.Id.ToString(),
                ["settlement_id"] = payout.SettlementId.ToString(),
                ["driver_id"] = withdrawal.DriverId.ToString(),
                ["withdrawal_id"] = withdrawal.Id.ToString()
            });
    }

    private static CreatePayoutCommand BuildCommand(
        Payout payout,
        string beneficiaryName,
        string beneficiaryIban,
        string? bankCode,
        string? mobile,
        string? city,
        string? purpose,
        IReadOnlyDictionary<string, string> metadata)
    {
        var sequenceNumber = payout.ProviderSequenceNumber ?? BuildSequenceNumber(payout.Id);

        return new CreatePayoutCommand(
            PayoutId: payout.Id,
            OwnerId: payout.Settlement.OwnerId,
            OwnerType: payout.Settlement.OwnerType.ToString(),
            Amount: payout.Amount,
            Currency: CurrencyPolicy.OfficialCurrency,
            IdempotencyKey: $"payout:{payout.Id:N}",
            BeneficiaryName: beneficiaryName,
            BeneficiaryIban: beneficiaryIban,
            BeneficiaryBankCode: bankCode,
            Reference: payout.TransferReference ?? sequenceNumber,
            Metadata: metadata,
            BeneficiaryMobile: mobile,
            BeneficiaryCountry: null,
            BeneficiaryCity: city,
            Purpose: purpose,
            SequenceNumber: sequenceNumber,
            Comment: $"Zadana payout {payout.Id:N}");
    }

    private static void ValidateGatewayCommand(CreatePayoutCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.BeneficiaryName))
        {
            throw new BusinessRuleException("PAYOUT_BENEFICIARY_NAME_REQUIRED", "Beneficiary account holder name is required.");
        }

        if (string.IsNullOrWhiteSpace(command.BeneficiaryIban))
        {
            throw new BusinessRuleException("PAYOUT_BENEFICIARY_IBAN_REQUIRED", "Beneficiary IBAN is required.");
        }

        if (string.IsNullOrWhiteSpace(command.BeneficiaryMobile))
        {
            throw new BusinessRuleException("PAYOUT_BENEFICIARY_MOBILE_REQUIRED", "Beneficiary mobile number is required.");
        }

        var iban = new string(command.BeneficiaryIban.Where(ch => !char.IsWhiteSpace(ch)).ToArray()).ToUpperInvariant();
        var country = string.IsNullOrWhiteSpace(command.BeneficiaryCountry)
            ? "SA"
            : command.BeneficiaryCountry.Trim().ToUpperInvariant();

        if (country == "SA" &&
            (iban.Length != 24 || !iban.StartsWith("SA", StringComparison.OrdinalIgnoreCase) || iban.Skip(2).Any(ch => !char.IsDigit(ch))))
        {
            throw new BusinessRuleException("PAYOUT_BENEFICIARY_IBAN_INVALID", "Beneficiary IBAN must be a valid Saudi IBAN.");
        }
    }

    private async Task MarkPaidCoreAsync(
        Payout payout,
        string transferReference,
        string? providerTransferId,
        string? providerName,
        string? providerSequenceNumber,
        CancellationToken cancellationToken)
    {
        await ExecuteInTransactionAsync(async () =>
        {
            payout.MarkAsPaid(transferReference, providerTransferId, providerName, providerSequenceNumber);
            payout.Settlement.MarkPaidOut();
            if (payout.ExecutionReservation?.IsAutomaticActive == true)
            {
                payout.ExecutionReservation.Confirm(Guid.Empty);
            }

            _context.PayoutAttempts.Add(new PayoutAttempt(
                payout.Id,
                PayoutAttemptType.ProviderCallback,
                PayoutStatus.Paid,
                providerName: payout.ProviderName,
                providerTransferId: providerTransferId ?? payout.ProviderTransferId,
                transferReference: transferReference,
                failureReason: null,
                rawPayload: null));

            await PostPayoutPaidAsync(payout, cancellationToken);
            await SettleVendorHoldIfApplicableAsync(payout, cancellationToken);
            await MarkLinkedDriverWithdrawalPaidAsync(payout.Id, transferReference, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        // Notify driver or vendor that their settlement/payout is complete
        await NotifyPayoutPaidAsync(payout, cancellationToken);
    }

    private async Task MarkManualPaidCoreAsync(
        Payout payout,
        string transferReference,
        Guid confirmedByUserId,
        CancellationToken cancellationToken)
    {
        await ExecuteInTransactionAsync(async () =>
        {
            payout.MarkAsManuallyPaid(transferReference, confirmedByUserId);
            payout.Settlement.MarkPaidOut();
            _context.PayoutAttempts.Add(new PayoutAttempt(
                payout.Id,
                PayoutAttemptType.ManualConfirmation,
                PayoutStatus.Paid,
                providerName: "Manual",
                transferReference: transferReference));

            await PostPayoutPaidAsync(payout, cancellationToken);
            await SettleVendorHoldIfApplicableAsync(payout, cancellationToken);
            await MarkLinkedDriverWithdrawalPaidAsync(payout.Id, transferReference, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        await NotifyPayoutPaidAsync(payout, cancellationToken);
    }

    private async Task MarkFailedCoreAsync(
        Payout payout,
        string failureReason,
        string? providerTransferId,
        string? providerName,
        string? providerSequenceNumber,
        CancellationToken cancellationToken)
    {
        await ExecuteInTransactionAsync(async () =>
        {
            payout.MarkAsFailed(failureReason, providerTransferId, providerName, providerSequenceNumber);
            payout.Settlement.MarkPayoutFailed();
            if (payout.ExecutionReservation?.IsAutomaticActive == true)
            {
                payout.ExecutionReservation.ReleaseAutomatic(failureReason);
            }

            await ReleaseVendorHoldIfApplicableAsync(payout, failureReason, cancellationToken);
            await MarkLinkedDriverWithdrawalFailedAsync(payout.Id, failureReason, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }

    private async Task MarkPayoutUnknownAsync(
        Payout payout,
        string providerName,
        string providerSequenceNumber,
        string reason,
        bool isRetry,
        CancellationToken cancellationToken)
    {
        payout.MarkAsProcessing(
            providerName: providerName,
            providerSequenceNumber: providerSequenceNumber);
        payout.Settlement.MarkAsProcessing();
        await MarkLinkedDriverWithdrawalProcessingAsync(payout.Id, cancellationToken);

        _context.PayoutAttempts.Add(new PayoutAttempt(
            payout.Id,
            isRetry ? PayoutAttemptType.Retry : PayoutAttemptType.Trigger,
            PayoutStatus.Processing,
            providerName: providerName,
            providerTransferId: payout.ProviderTransferId,
            transferReference: payout.TransferReference,
            failureReason: $"Unknown provider state: {reason}"));

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkLinkedDriverWithdrawalProcessingAsync(Guid payoutId, CancellationToken cancellationToken)
    {
        var withdrawal = await _context.DriverWithdrawalRequests
            .FirstOrDefaultAsync(item => item.PayoutId == payoutId, cancellationToken);

        if (withdrawal is not null && withdrawal.Status != DriverWithdrawalStatus.Paid)
        {
            withdrawal.MarkProcessing();
        }
    }

    private async Task<bool> ReserveAutomaticSubmissionAsync(
        Payout payout,
        string providerName,
        string? providerSequenceNumber,
        bool isRetry,
        CancellationToken cancellationToken)
    {
        // Re-read the persisted setting at the execution boundary.  Trigger
        // can spend time building the gateway command, during which finance
        // may switch the platform to Manual.  The durable reservation is the
        // final automatic-dispatch boundary: after it is saved the payout is
        // reconciliation-only, so it can never be submitted twice.
        if (!await IsAutomaticProcessingEnabledAsync(cancellationToken))
        {
            return false;
        }

        var reservation = payout.ExecutionReservation;
        if (reservation?.IsActive == true)
        {
            return false;
        }

        if (reservation is null)
        {
            reservation = new PayoutExecutionReservation(payout.Id, PayoutExecutionMode.Automatic);
            _context.PayoutExecutionReservations.Add(reservation);
        }
        else
        {
            reservation.ReclaimAutomatic();
        }

        // Submitted is intentionally persisted before the provider POST. This
        // is the durable "do not submit again" barrier if a process terminates
        // after the bank gateway accepts the command but before it responds.
        reservation.MarkSubmitted();
        payout.MarkAsProcessing(
            providerName: providerName,
            providerSequenceNumber: providerSequenceNumber);
        payout.Settlement.MarkAsProcessing();
        await MarkLinkedDriverWithdrawalProcessingAsync(payout.Id, cancellationToken);

        _context.PayoutAttempts.Add(new PayoutAttempt(
            payout.Id,
            isRetry ? PayoutAttemptType.Retry : PayoutAttemptType.Trigger,
            PayoutStatus.Processing,
            providerName: providerName,
            transferReference: payout.TransferReference));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another worker/admin acquired the payout between the initial
            // read and this save. Most importantly, do not call the gateway.
            DetachExecutionChanges(payout.Id);
            return false;
        }
        catch (DbUpdateException ex) when (IsExecutionReservationUniqueConflict(ex))
        {
            // The unique PayoutId reservation index is the database-level
            // backstop for concurrent initial claims.
            DetachExecutionChanges(payout.Id);
            return false;
        }
    }

    private static void EnsureManualClaimIsSafe(Payout payout)
    {
        if (!string.IsNullOrWhiteSpace(payout.ProviderTransferId) ||
            (payout.Status is PayoutStatus.Queued or PayoutStatus.Processing &&
             !string.Equals(payout.ProviderName, "Manual", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleException(
                "PAYOUT_PROVIDER_RECONCILIATION_REQUIRED",
                "A gateway payout must be reconciled before it can be claimed for manual transfer.");
        }

        if (payout.Status is not (PayoutStatus.Pending or PayoutStatus.Failed))
        {
            throw new BusinessRuleException(
                "PAYOUT_INVALID_STATUS",
                $"Cannot claim payout from status {payout.Status}.");
        }
    }

    private async Task SaveReservationChangesAsync(Guid payoutId, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            DetachExecutionChanges(payoutId);
            throw new BusinessRuleException(
                "PAYOUT_CONCURRENTLY_UPDATED",
                "This payout was changed by another finance operation. Refresh it before trying again.");
        }
        catch (DbUpdateException ex) when (IsExecutionReservationUniqueConflict(ex))
        {
            DetachExecutionChanges(payoutId);
            throw new BusinessRuleException(
                "PAYOUT_ALREADY_RESERVED",
                "This payout was claimed by another finance operation. Refresh it before trying again.");
        }
    }

    private async Task<bool> RequiresManualPayoutDualControlAsync(CancellationToken cancellationToken)
    {
        if (_settlementProcessingSettingsService is null)
        {
            return true;
        }

        return (await _settlementProcessingSettingsService.GetAsync(cancellationToken))
            .RequireManualPayoutDualControl;
    }

    private async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        if (_context is not DbContext dbContext)
        {
            await action();
            return;
        }

        // The unit-test context uses EF InMemory, which deliberately does not
        // implement transactions. SQL Server/SQLite providers both expose a
        // provider name other than InMemory and use the real transaction path.
        if (string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.OrdinalIgnoreCase))
        {
            await action();
            return;
        }

        // Do not create a nested transaction when the caller already owns
        // the unit of work. The outer caller is then responsible for running
        // its transaction through the appropriate execution strategy.
        if (dbContext.Database.CurrentTransaction is not null)
        {
            await action();
            return;
        }

        // SQL Server has a retrying execution strategy in production. EF Core
        // requires an explicitly opened transaction to be created *inside*
        // that strategy; otherwise any query performed by the financial
        // posting service fails before the transaction body can run.
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await action();
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private void DetachExecutionChanges(Guid payoutId)
    {
        if (_context is not DbContext dbContext)
        {
            return;
        }

        // A failed reservation save may have changed the tracked settlement
        // to Processing.  Detach it with the payout graph as well, otherwise a
        // later unrelated SaveChanges in the same request could accidentally
        // persist a stale status after another worker won the reservation.
        var settlementIds = dbContext.ChangeTracker.Entries<Payout>()
            .Where(entry => entry.Entity.Id == payoutId)
            .Select(entry => entry.Entity.SettlementId)
            .ToHashSet();

        foreach (var entry in dbContext.ChangeTracker.Entries()
                     .Where(entry =>
                         entry.Entity is Payout payout && payout.Id == payoutId ||
                         entry.Entity is Settlement settlement && settlementIds.Contains(settlement.Id) ||
                         entry.Entity is PayoutExecutionReservation reservation && reservation.PayoutId == payoutId ||
                         entry.Entity is PayoutAttempt attempt && attempt.PayoutId == payoutId ||
                         entry.Entity is PayoutManualConfirmation confirmation && confirmation.PayoutId == payoutId ||
                         entry.Entity is PayoutReversal reversal && reversal.PayoutId == payoutId ||
                         entry.Entity is PayoutProofAttachment attachment && attachment.PayoutId == payoutId ||
                         entry.Entity is DriverWithdrawalRequest withdrawal && withdrawal.PayoutId == payoutId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static bool IsExecutionReservationUniqueConflict(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(
            "IX_PayoutExecutionReservations_PayoutId",
            StringComparison.OrdinalIgnoreCase) == true ||
        exception.Message.Contains(
            "PayoutExecutionReservations.PayoutId",
            StringComparison.OrdinalIgnoreCase);

    private static void EnsureSettlementCanBeTriggered(Payout payout)
    {
        if (payout.Settlement.Origin == SettlementOrigin.DirectPerOrder)
        {
            return;
        }

        if (payout.Settlement.Status is SettlementStatus.Approved or SettlementStatus.PayoutFailed)
        {
            return;
        }

        // Once a manual transfer has been recorded as submitted, the
        // settlement is intentionally Processing. It remains eligible only for
        // its owning manual reservation to complete; this is not a gateway
        // retry escape hatch.
        if (payout.Settlement.Status == SettlementStatus.Processing &&
            payout.ExecutionReservation?.Mode == PayoutExecutionMode.Manual &&
            payout.ExecutionReservation.Status == PayoutExecutionReservationStatus.Submitted)
        {
            return;
        }

        throw new BusinessRuleException(
            "SETTLEMENT_APPROVAL_REQUIRED",
            "Scheduled and manual settlements must be approved by finance before payout can be triggered.");
    }

    private static void EnsureManualConfirmationIsSafe(Payout payout)
    {
        if (!string.IsNullOrWhiteSpace(payout.ProviderTransferId))
        {
            throw new BusinessRuleException(
                "PAYOUT_PROVIDER_RECONCILIATION_REQUIRED",
                "This payout already has a gateway transfer reference and must be reconciled with the provider before manual confirmation.");
        }

        var isEligibleStatus = payout.Status is PayoutStatus.Pending or PayoutStatus.Failed ||
            (payout.Status is PayoutStatus.Queued or PayoutStatus.Processing &&
             string.Equals(payout.ProviderName, "Manual", StringComparison.OrdinalIgnoreCase));

        if (!isEligibleStatus)
        {
            throw new BusinessRuleException(
                "PAYOUT_INVALID_STATUS",
                $"Cannot manually confirm payout from status {payout.Status}.");
        }

        if (payout.Status is PayoutStatus.Queued or PayoutStatus.Processing &&
            !string.Equals(payout.ProviderName, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "PAYOUT_PROVIDER_RECONCILIATION_REQUIRED",
                "An in-flight gateway payout must be reconciled with the provider before manual confirmation.");
        }
    }

    private async Task EnsureManualConfirmationIsDueTodayAsync(
        Payout payout,
        CancellationToken cancellationToken)
    {
        var today = SaudiTime.Today;
        var enabledPayoutDays = _settlementProcessingSettingsService is null
            ? PayoutScheduleDayPolicy.DefaultPayoutDays
            : await _settlementProcessingSettingsService.GetEnabledPayoutDaysAsync(cancellationToken);
        var todayPayoutDay = (PayoutScheduleDay)today.DayOfWeek;
        if (!enabledPayoutDays.Contains(todayPayoutDay))
        {
            throw new BusinessRuleException(
                "PAYOUT_CONFIRMATION_DAY_INVALID",
                "Manual payout confirmation is only allowed on an enabled payout day.");
        }

        var payoutDay = await ResolvePayoutDayAsync(payout, cancellationToken);

        if (!payoutDay.HasValue)
        {
            throw new BusinessRuleException(
                "PAYOUT_OWNER_NOT_FOUND",
                "The payout owner could not be found while checking the payout day.");
        }

        if (!enabledPayoutDays.Contains(payoutDay.Value))
        {
            throw new BusinessRuleException(
                "PAYOUT_DAY_DISABLED",
                "The payout owner's selected day is not enabled by the platform.");
        }

        if (!PayoutScheduleDayPolicy.IsPayoutDay(today, payoutDay.Value))
        {
            throw new BusinessRuleException(
                "PAYOUT_NOT_DUE_TODAY",
                $"This payout is scheduled for {payoutDay.Value}.");
        }
    }

    private async Task<bool> IsAutomaticPayoutDueTodayAsync(
        Payout payout,
        CancellationToken cancellationToken)
    {
        var enabledPayoutDays = _settlementProcessingSettingsService is null
            ? PayoutScheduleDayPolicy.DefaultPayoutDays
            : await _settlementProcessingSettingsService.GetEnabledPayoutDaysAsync(cancellationToken);
        var today = SaudiTime.Today;
        var todayPayoutDay = (PayoutScheduleDay)today.DayOfWeek;
        if (!enabledPayoutDays.Contains(todayPayoutDay))
        {
            return false;
        }

        var payoutDay = await ResolvePayoutDayAsync(payout, cancellationToken);

        // A missing owner remains pending for operational review. This guard
        // stays non-throwing so workers do not emit repeated error logs for an
        // off-cycle or orphaned pending payout.
        return payoutDay.HasValue &&
               enabledPayoutDays.Contains(payoutDay.Value) &&
               PayoutScheduleDayPolicy.IsPayoutDay(today, payoutDay.Value);
    }

    /// <summary>
    /// New payouts own an immutable schedule snapshot. The owner lookup is a
    /// backwards-compatible fallback for legacy rows created before that field
    /// existed; it must never override an already prepared payout.
    /// </summary>
    private async Task<PayoutScheduleDay?> ResolvePayoutDayAsync(
        Payout payout,
        CancellationToken cancellationToken)
    {
        if (payout.ScheduledPayoutDay.HasValue)
        {
            return payout.ScheduledPayoutDay.Value;
        }

        return payout.Settlement.OwnerType switch
        {
            SettlementOwnerType.Vendor => await _context.Vendors
                .AsNoTracking()
                .Where(item => item.Id == payout.Settlement.OwnerId)
                .Select(item => (PayoutScheduleDay?)item.PayoutDay)
                .FirstOrDefaultAsync(cancellationToken),
            SettlementOwnerType.Driver => await _context.Drivers
                .AsNoTracking()
                .Where(item => item.Id == payout.Settlement.OwnerId)
                .Select(item => (PayoutScheduleDay?)item.PayoutDay)
                .FirstOrDefaultAsync(cancellationToken),
            _ => null
        };
    }

    private async Task MarkLinkedDriverWithdrawalPaidAsync(Guid payoutId, string transferReference, CancellationToken cancellationToken)
    {
        var withdrawal = await _context.DriverWithdrawalRequests
            .FirstOrDefaultAsync(item => item.PayoutId == payoutId, cancellationToken);

        if (withdrawal is null)
        {
            return;
        }

        withdrawal.MarkPaid(transferReference);
        var holds = await LoadActiveWithdrawalHoldsAsync(withdrawal, cancellationToken);
        foreach (var hold in holds)
        {
            hold.Consume();
        }
    }

    private async Task MarkLinkedDriverWithdrawalFailedAsync(Guid payoutId, string failureReason, CancellationToken cancellationToken)
    {
        var withdrawal = await _context.DriverWithdrawalRequests
            .FirstOrDefaultAsync(item => item.PayoutId == payoutId, cancellationToken);

        if (withdrawal is null)
        {
            return;
        }

        withdrawal.MarkFailed(failureReason);
        var holds = await LoadActiveWithdrawalHoldsAsync(withdrawal, cancellationToken);
        foreach (var hold in holds)
        {
            hold.Cancel(failureReason);
        }
    }

    private async Task CancelLinkedDriverWithdrawalAsync(Guid payoutId, string reason, CancellationToken cancellationToken)
    {
        var withdrawal = await _context.DriverWithdrawalRequests
            .FirstOrDefaultAsync(item => item.PayoutId == payoutId, cancellationToken);

        if (withdrawal is null)
        {
            return;
        }

        withdrawal.Cancel(reason);
        var holds = await LoadActiveWithdrawalHoldsAsync(withdrawal, cancellationToken);
        foreach (var hold in holds)
        {
            hold.Cancel(reason);
        }
    }

    private async Task<List<WalletHold>> LoadActiveWithdrawalHoldsAsync(
        DriverWithdrawalRequest withdrawal,
        CancellationToken cancellationToken)
    {
        return await _context.WalletHolds
            .Where(item =>
                item.OwnerType == WalletOwnerType.Driver &&
                item.OwnerId == withdrawal.DriverId &&
                item.Reason == WalletHoldReason.Withdrawal &&
                item.Status == WalletHoldStatus.Active &&
                item.ReferenceType == "DriverWithdrawalRequest" &&
                item.ReferenceId == withdrawal.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task SettleVendorHoldIfApplicableAsync(Payout payout, CancellationToken cancellationToken)
    {
        if (payout.Settlement.OwnerType != SettlementOwnerType.Vendor)
        {
            return;
        }

        await _vendorPayoutWalletService.SettleHoldAsync(
            payout.Settlement.OwnerId,
            payout.SettlementId,
            payout.Id,
            payout.Amount,
            $"Payout paid {payout.Id}",
            cancellationToken);
    }

    private async Task ReleaseVendorHoldIfApplicableAsync(
        Payout payout,
        string reason,
        CancellationToken cancellationToken)
    {
        if (payout.Settlement.OwnerType != SettlementOwnerType.Vendor)
        {
            return;
        }

        await _vendorPayoutWalletService.ReleaseHoldAsync(
            payout.Settlement.OwnerId,
            payout.SettlementId,
            payout.Amount,
            "PayoutFailedRelease",
            reason,
            cancellationToken);
    }

    private async Task PostPayoutPaidAsync(Payout payout, CancellationToken cancellationToken)
    {
        var settlement = payout.Settlement;
        var payableAccount = settlement.OwnerType == SettlementOwnerType.Driver
            ? FinancialAccountCode.DriverPayable
            : FinancialAccountCode.VendorPayable;
        var ownerType = settlement.OwnerType == SettlementOwnerType.Driver
            ? FinancialOwnerType.Driver
            : FinancialOwnerType.Vendor;
        var eventType = settlement.OwnerType == SettlementOwnerType.Driver
            ? FinancialEventType.DriverPayoutPaid
            : FinancialEventType.VendorPayoutPaid;

        var result = await _postingService.PostAsync(
            eventType,
            BuildPayoutPaidIdempotencyKey(payout),
            [
                new JournalLineDraft(
                    payableAccount,
                    payout.Amount,
                    0m,
                    ownerType,
                    settlement.OwnerId,
                    SettlementId: settlement.Id,
                    PayoutId: payout.Id,
                    Memo: $"Payout paid {payout.Id}"),
                new JournalLineDraft(
                    FinancialAccountCode.PlatformCash,
                    0m,
                    payout.Amount,
                    FinancialOwnerType.Platform,
                    _settings.PlatformWalletOwnerId,
                    SettlementId: settlement.Id,
                    PayoutId: payout.Id,
                    Memo: $"Platform cash payout {payout.Id}")
            ],
            settlementId: settlement.Id,
            payoutId: payout.Id,
            currencyCode: CurrencyPolicy.OfficialCurrency,
            description: $"Payout paid {payout.Id}",
            cancellationToken: cancellationToken);

        await _walletProjectionUpdater.ApplyJournalEntryAsync(result.JournalEntryId, cancellationToken);
    }

    private static string BuildPayoutPaidIdempotencyKey(Payout payout)
    {
        var providerReference = payout.ProviderTransferId
            ?? payout.ProviderSequenceNumber
            ?? payout.TransferReference
            ?? string.Empty;
        var legacyKey = $"payout-paid:{payout.Id:N}:{providerReference}";

        // Preserving fitting legacy keys avoids changing the idempotency value
        // of payouts which may already have a financial event in production.
        if (legacyKey.Length <= FinancialEventIdempotencyKeyMaxLength)
        {
            return legacyKey;
        }

        var referenceHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(providerReference)));
        return $"payout-paid:{payout.Id:N}:sha256:{referenceHash}";
    }

    private async Task PostPayoutReversedAsync(
        Payout payout,
        string returnReference,
        CancellationToken cancellationToken)
    {
        var settlement = payout.Settlement;
        var payableAccount = settlement.OwnerType == SettlementOwnerType.Driver
            ? FinancialAccountCode.DriverPayable
            : FinancialAccountCode.VendorPayable;
        var ownerType = settlement.OwnerType == SettlementOwnerType.Driver
            ? FinancialOwnerType.Driver
            : FinancialOwnerType.Vendor;
        var eventType = settlement.OwnerType == SettlementOwnerType.Driver
            ? FinancialEventType.DriverPayoutReversed
            : FinancialEventType.VendorPayoutReversed;

        var result = await _postingService.PostAsync(
            eventType,
            $"payout-reversal:{payout.Id:N}",
            [
                new JournalLineDraft(
                    FinancialAccountCode.PlatformCash,
                    payout.Amount,
                    0m,
                    FinancialOwnerType.Platform,
                    _settings.PlatformWalletOwnerId,
                    SettlementId: settlement.Id,
                    PayoutId: payout.Id,
                    Memo: $"Returned payout {payout.Id}"),
                new JournalLineDraft(
                    payableAccount,
                    0m,
                    payout.Amount,
                    ownerType,
                    settlement.OwnerId,
                    SettlementId: settlement.Id,
                    PayoutId: payout.Id,
                    Memo: $"Payout return payable restoration {payout.Id}")
            ],
            settlementId: settlement.Id,
            payoutId: payout.Id,
            currencyCode: CurrencyPolicy.OfficialCurrency,
            description: $"Payout return recorded {payout.Id}",
            cancellationToken: cancellationToken);

        await _walletProjectionUpdater.ApplyJournalEntryAsync(result.JournalEntryId, cancellationToken);
    }

    private IPayoutGateway? GetEnabledGateway(string? providerName = null)
    {
        var enabled = _payoutGateways.Where(gateway => gateway.IsEnabled);

        if (!string.IsNullOrWhiteSpace(providerName))
        {
            return enabled.FirstOrDefault(gateway =>
                string.Equals(gateway.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        }

        return enabled.FirstOrDefault();
    }

    private static bool IsPaid(string status) =>
        string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase);

    private static bool IsPending(string status) =>
        string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "initiated", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "accepted", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "processing", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnknown(string status) =>
        string.Equals(status, "unknown", StringComparison.OrdinalIgnoreCase);

    private static bool IsTransientIntegrationException(Exception exception) =>
        exception is TimeoutException ||
        exception is HttpRequestException ||
        exception is TaskCanceledException ||
        (exception is ExternalServiceException &&
            !exception.Message.Contains("400", StringComparison.OrdinalIgnoreCase) &&
            !exception.Message.Contains("401", StringComparison.OrdinalIgnoreCase) &&
            !exception.Message.Contains("403", StringComparison.OrdinalIgnoreCase) &&
            !exception.Message.Contains("404", StringComparison.OrdinalIgnoreCase) &&
            !exception.Message.Contains("422", StringComparison.OrdinalIgnoreCase));

    private static PayoutStatus MapProviderStatus(string providerStatus)
    {
        if (IsPaid(providerStatus))
        {
            return PayoutStatus.Paid;
        }

        if (!IsPending(providerStatus) && !IsUnknown(providerStatus))
        {
            return PayoutStatus.Failed;
        }

        return string.Equals(providerStatus, "queued", StringComparison.OrdinalIgnoreCase)
            ? PayoutStatus.Queued
            : PayoutStatus.Processing;
    }

    private static void ApplyProviderPendingStatus(
        Payout payout,
        string providerStatus,
        string? providerTransferId,
        string? providerName,
        string? providerSequenceNumber)
    {
        if (string.Equals(providerStatus, "queued", StringComparison.OrdinalIgnoreCase))
        {
            payout.MarkQueued(providerTransferId, providerName, providerSequenceNumber);
            return;
        }

        payout.MarkAsProcessing(providerTransferId, providerName, providerSequenceNumber);
    }

    private static string BuildSequenceNumber(Guid payoutId)
    {
        var digits = new string(payoutId.ToString("N").Where(char.IsDigit).ToArray());
        if (digits.Length >= 16)
        {
            return digits[..16];
        }

        return digits.PadRight(16, '0');
    }

    private Task SendPayoutRequiresReviewAlertAsync(Payout payout, string reason, CancellationToken cancellationToken)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "Payout status is unknown and needs reconciliation."
            : reason.Trim();

        return _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.PayoutRequiresReview,
                AdminAlertCategories.Settlements,
                AdminAlertPriorities.High,
                "Payout requires review",
                "Payout requires review",
                $"Payout {payout.Id} for {payout.Amount:N2} needs provider reconciliation. Reason: {normalizedReason}",
                $"Payout {payout.Id} for {payout.Amount:N2} needs provider reconciliation. Reason: {normalizedReason}",
                payout.Id,
                "/finances/withdrawals",
                new
                {
                    payoutId = payout.Id,
                    settlementId = payout.SettlementId,
                    amount = payout.Amount,
                    status = payout.Status.ToString(),
                    providerName = payout.ProviderName,
                    providerTransferId = payout.ProviderTransferId,
                    providerSequenceNumber = payout.ProviderSequenceNumber,
                    reason = normalizedReason
                }),
            cancellationToken);
    }

    private Task SendPayoutFailedAlertAsync(Payout payout, string? failureReason, CancellationToken cancellationToken)
    {
        var reason = string.IsNullOrWhiteSpace(failureReason) ? "Provider did not return a failure reason." : failureReason.Trim();

        return _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.SettlementFailed,
                AdminAlertCategories.Settlements,
                AdminAlertPriorities.Critical,
                "Settlement payout failed",
                "Settlement payout failed",
                $"A settlement payout for {payout.Amount:N2} failed. Reason: {reason}",
                $"A settlement payout for {payout.Amount:N2} failed. Reason: {reason}",
                payout.Id,
                "/finances/settlements",
                new
                {
                    payoutId = payout.Id,
                    settlementId = payout.SettlementId,
                    amount = payout.Amount,
                    providerTransferId = payout.ProviderTransferId,
                    providerSequenceNumber = payout.ProviderSequenceNumber,
                    transferReference = payout.TransferReference,
                    failureReason = reason
                }),
            cancellationToken);
    }

    private Task SendPayoutIntegrationFailureAlertAsync(Payout payout, Exception exception, CancellationToken cancellationToken)
    {
        return _adminAlertService.SendAsync(
            new AdminAlertRequest(
                AdminAlertTypes.SystemIntegrationFailure,
                AdminAlertCategories.System,
                AdminAlertPriorities.Critical,
                "Payout integration failure",
                "Payout integration failure",
                $"Payout trigger failed for settlement {payout.SettlementId}.",
                $"Payout trigger failed for settlement {payout.SettlementId}.",
                payout.Id,
                "/finances/settlements",
                new
                {
                    payoutId = payout.Id,
                    settlementId = payout.SettlementId,
                    amount = payout.Amount,
                    exceptionType = exception.GetType().Name,
                    message = exception.Message
                }),
            cancellationToken);
    }

    private async Task NotifyPayoutPaidAsync(Payout payout, CancellationToken cancellationToken)
    {
        try
        {
            var settlement = payout.Settlement;

            if (settlement.OwnerType == SettlementOwnerType.Driver)
            {
                // Find the driver's user ID via linked withdrawal request
                var driverUserId = await _context.DriverWithdrawalRequests
                    .AsNoTracking()
                    .Where(w => w.PayoutId == payout.Id)
                    .Join(_context.Drivers.AsNoTracking(),
                        w => w.DriverId,
                        d => d.Id,
                        (w, d) => d.UserId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (driverUserId == Guid.Empty)
                {
                    return;
                }

                var data = DriverNotificationDataBuilder.Build(
                    screen: "wallet",
                    @event: "wallet.payout_completed",
                    extra: new
                    {
                        payoutId = payout.Id,
                        settlementId = payout.SettlementId,
                        amount = payout.Amount,
                        transferReference = payout.TransferReference
                    });

                await _notificationService.SendToUserAsync(
                    driverUserId,
                    "أكملنا التحويل إلى حسابك البنكي",
                    "Payout completed",
                    $"حوّلنا مبلغ {payout.Amount:0.00} ريال إلى حسابك البنكي بنجاح.",
                    $"A payout of {payout.Amount:0.00} SAR has been successfully transferred to your bank account.",
                    NotificationTypes.DriverWalletUpdated,
                    payout.Id,
                    data,
                    cancellationToken);

                await _notificationService.SendDriverWalletUpdatedAsync(driverUserId, cancellationToken);

                await _oneSignalPushService.SendMobileNotificationAsync(
                    OneSignalMobilePushRequest.CreateHeadsUp(
                        driverUserId.ToString(),
                        "\u062a\u0645 \u0625\u062a\u0645\u0627\u0645 \u0627\u0644\u062a\u062d\u0648\u064a\u0644 \u0625\u0644\u0649 \u062d\u0633\u0627\u0628\u0643 \u0627\u0644\u0628\u0646\u0643\u064a",
                        "Payout completed",
                        $"\u062a\u0645 \u062a\u062d\u0648\u064a\u0644 \u0645\u0628\u0644\u063a {payout.Amount:0.00} \u0631\u064a\u0627\u0644 \u0625\u0644\u0649 \u062d\u0633\u0627\u0628\u0643 \u0627\u0644\u0628\u0646\u0643\u064a \u0628\u0646\u062c\u0627\u062d.",
                        $"A payout of {payout.Amount:0.00} SAR has been successfully transferred to your bank account.",
                        NotificationTypes.DriverWalletUpdated,
                        payout.Id,
                        data,
                        "/wallet",
                        NotificationCategories.Wallet,
                        OneSignalApplicationTarget.Driver),
                    cancellationToken);
            }
            else if (settlement.OwnerType == SettlementOwnerType.Vendor)
            {
                var vendorUserId = await _context.Vendors
                    .AsNoTracking()
                    .Where(v => v.Id == settlement.OwnerId)
                    .Select(v => v.UserId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (vendorUserId == Guid.Empty)
                {
                    return;
                }

                var data = $"{{\"payoutId\":\"{payout.Id}\",\"settlementId\":\"{payout.SettlementId}\",\"amount\":{payout.Amount},\"transferReference\":\"{payout.TransferReference}\",\"targetUrl\":\"/finances/settlements\"}}";

                await _notificationService.SendToUserAsync(
                    vendorUserId,
                    "صرفنا مستحقاتك",
                    "Settlement paid",
                    $"حوّلنا مستحقاتك بمبلغ {payout.Amount:0.00} ريال إلى حسابك البنكي.",
                    $"Your settlement of {payout.Amount:0.00} SAR has been transferred to your bank account.",
                    NotificationTypes.VendorSettlementPaid,
                    payout.Id,
                    data,
                    cancellationToken);

                await _oneSignalPushService.SendToExternalUserAsync(
                    vendorUserId.ToString(),
                    "صرفنا مستحقاتك",
                    "Settlement paid",
                    $"حوّلنا مستحقاتك بمبلغ {payout.Amount:0.00} ريال إلى حسابك البنكي.",
                    $"Your settlement of {payout.Amount:0.00} SAR has been transferred to your bank account.",
                    NotificationTypes.VendorSettlementPaid,
                    payout.Id,
                    data,
                    "/finances/settlements",
                    cancellationToken);
            }
        }
        catch
        {
            // Notification failures must never break the payout flow
        }
    }
}
