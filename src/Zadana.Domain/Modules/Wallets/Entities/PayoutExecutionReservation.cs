using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Wallets.Entities;

/// <summary>
/// Durable, one-to-one ownership record for payout execution. It is deliberately
/// independent from <see cref="PayoutStatus"/>: a pending payout can be claimed
/// for a future manual bank transfer without pretending that the transfer has
/// already been sent.
/// </summary>
public sealed class PayoutExecutionReservation : BaseEntity
{
    public Guid PayoutId { get; private set; }
    public PayoutExecutionMode Mode { get; private set; }
    public PayoutExecutionReservationStatus Status { get; private set; }
    public Guid? ClaimedByUserId { get; private set; }
    public DateTime ClaimedAtUtc { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public string? SubmissionReference { get; private set; }
    public Guid? ReleasedByUserId { get; private set; }
    public DateTime? ReleasedAtUtc { get; private set; }
    public string? ReleaseReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public Payout Payout { get; private set; } = null!;

    public bool IsActive => Status is PayoutExecutionReservationStatus.Claimed or PayoutExecutionReservationStatus.Submitted;
    public bool IsManualActive => IsActive && Mode == PayoutExecutionMode.Manual;
    public bool IsAutomaticActive => IsActive && Mode == PayoutExecutionMode.Automatic;
    public bool HasBeenSubmitted => Status is PayoutExecutionReservationStatus.Submitted or PayoutExecutionReservationStatus.Confirmed;

    private PayoutExecutionReservation() { }

    public PayoutExecutionReservation(
        Guid payoutId,
        PayoutExecutionMode mode,
        Guid? claimedByUserId = null)
    {
        if (payoutId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_REQUIRED", "Payout is required for an execution reservation.");
        }

        if (mode == PayoutExecutionMode.Manual && (!claimedByUserId.HasValue || claimedByUserId == Guid.Empty))
        {
            throw new BusinessRuleException("PAYOUT_CLAIMING_USER_REQUIRED", "The administrator claiming a manual payout is required.");
        }

        PayoutId = payoutId;
        Mode = mode;
        Status = PayoutExecutionReservationStatus.Claimed;
        ClaimedByUserId = mode == PayoutExecutionMode.Manual ? claimedByUserId : null;
        ClaimedAtUtc = DateTime.UtcNow;
    }

    public void ReclaimManual(Guid claimedByUserId)
    {
        EnsureReleased();
        if (claimedByUserId == Guid.Empty)
        {
            throw new BusinessRuleException("PAYOUT_CLAIMING_USER_REQUIRED", "The administrator claiming a manual payout is required.");
        }

        Mode = PayoutExecutionMode.Manual;
        Status = PayoutExecutionReservationStatus.Claimed;
        ClaimedByUserId = claimedByUserId;
        ClaimedAtUtc = DateTime.UtcNow;
        SubmittedByUserId = null;
        SubmittedAtUtc = null;
        SubmissionReference = null;
        ReleasedByUserId = null;
        ReleasedAtUtc = null;
        ReleaseReason = null;
    }

    public void ReclaimAutomatic()
    {
        EnsureReleased();

        Mode = PayoutExecutionMode.Automatic;
        Status = PayoutExecutionReservationStatus.Claimed;
        ClaimedByUserId = null;
        ClaimedAtUtc = DateTime.UtcNow;
        SubmittedByUserId = null;
        SubmittedAtUtc = null;
        SubmissionReference = null;
        ReleasedByUserId = null;
        ReleasedAtUtc = null;
        ReleaseReason = null;
    }

    public void MarkSubmitted(Guid? submittedByUserId = null, string? submissionReference = null)
    {
        if (Status != PayoutExecutionReservationStatus.Claimed)
        {
            throw new BusinessRuleException("PAYOUT_RESERVATION_INVALID_STATUS", "Only a claimed payout can be submitted.");
        }

        if (Mode == PayoutExecutionMode.Manual)
        {
            if (!submittedByUserId.HasValue || submittedByUserId == Guid.Empty)
            {
                throw new BusinessRuleException("PAYOUT_SUBMITTING_USER_REQUIRED", "The administrator submitting the manual bank transfer is required.");
            }

            if (ClaimedByUserId != submittedByUserId)
            {
                throw new BusinessRuleException("PAYOUT_CLAIM_OWNERSHIP_REQUIRED", "Only the administrator who claimed the payout can record its bank submission.");
            }

            if (string.IsNullOrWhiteSpace(submissionReference))
            {
                throw new BusinessRuleException("BANK_SUBMISSION_REFERENCE_REQUIRED", "Bank submission reference is required before manual payout confirmation.");
            }
        }

        Status = PayoutExecutionReservationStatus.Submitted;
        SubmittedByUserId = submittedByUserId;
        SubmittedAtUtc = DateTime.UtcNow;
        SubmissionReference = string.IsNullOrWhiteSpace(submissionReference) ? null : submissionReference.Trim();
    }

    public void Confirm(Guid confirmedByUserId)
    {
        if (Status != PayoutExecutionReservationStatus.Submitted)
        {
            throw new BusinessRuleException("PAYOUT_RESERVATION_NOT_SUBMITTED", "The payout must be recorded as submitted before it can be confirmed.");
        }

        Status = PayoutExecutionReservationStatus.Confirmed;
    }

    public void Release(Guid? releasedByUserId, string reason)
    {
        if (Status != PayoutExecutionReservationStatus.Claimed)
        {
            throw new BusinessRuleException(
                "PAYOUT_RESERVATION_RECONCILIATION_REQUIRED",
                "A submitted payout cannot be released. Reconcile or reverse it instead.");
        }

        if (Mode == PayoutExecutionMode.Manual && ClaimedByUserId != releasedByUserId)
        {
            throw new BusinessRuleException("PAYOUT_CLAIM_OWNERSHIP_REQUIRED", "Only the administrator who claimed the payout can release it.");
        }

        Status = PayoutExecutionReservationStatus.Released;
        ReleasedByUserId = releasedByUserId;
        ReleasedAtUtc = DateTime.UtcNow;
        ReleaseReason = string.IsNullOrWhiteSpace(reason) ? "Payout reservation released." : reason.Trim();
    }

    /// <summary>
    /// Finance cancellation is an administrative operation and may be performed
    /// by an approver other than the original claimant, but only before any
    /// external submission exists.
    /// </summary>
    public void ReleaseForCancellation(Guid? releasedByUserId, string reason)
    {
        if (Status != PayoutExecutionReservationStatus.Claimed)
        {
            throw new BusinessRuleException(
                "PAYOUT_RESERVATION_RECONCILIATION_REQUIRED",
                "A submitted payout cannot be cancelled. Reconcile or reverse it instead.");
        }

        Status = PayoutExecutionReservationStatus.Released;
        ReleasedByUserId = releasedByUserId;
        ReleasedAtUtc = DateTime.UtcNow;
        ReleaseReason = string.IsNullOrWhiteSpace(reason) ? "Payout reservation cancelled." : reason.Trim();
    }

    public void ReleaseAutomatic(string reason)
    {
        if (Mode != PayoutExecutionMode.Automatic || Status is not (PayoutExecutionReservationStatus.Claimed or PayoutExecutionReservationStatus.Submitted))
        {
            throw new BusinessRuleException("PAYOUT_RESERVATION_INVALID_STATUS", "Only an active automatic reservation can be released by provider reconciliation.");
        }

        Status = PayoutExecutionReservationStatus.Released;
        ReleasedByUserId = null;
        ReleasedAtUtc = DateTime.UtcNow;
        ReleaseReason = string.IsNullOrWhiteSpace(reason) ? "Gateway payout failed." : reason.Trim();
    }

    private void EnsureReleased()
    {
        if (Status != PayoutExecutionReservationStatus.Released)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_RESERVED", "The payout already has an active or completed execution reservation.");
        }
    }
}
