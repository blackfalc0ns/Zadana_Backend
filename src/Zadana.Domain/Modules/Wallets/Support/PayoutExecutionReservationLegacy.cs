namespace Zadana.Domain.Modules.Wallets.Support;

public static class PayoutExecutionReservationLegacy
{
    public const string BackfillSubmissionReference = "Legacy manual payout awaiting confirmation";

    public static bool IsBackfilledSubmission(
        string? submissionReference,
        Guid? claimedByUserId,
        Guid? submittedByUserId) =>
        claimedByUserId is null &&
        submittedByUserId is null &&
        string.Equals(
            submissionReference?.Trim(),
            BackfillSubmissionReference,
            StringComparison.Ordinal);
}
