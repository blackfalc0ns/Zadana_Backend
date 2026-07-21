using System.Text.Json;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Finances.Services;

/// <summary>
/// Serializes the recipient details captured at payout preparation time. The
/// serialized value is stored in <see cref="Payout.DestinationSnapshot"/>,
/// which is encrypted by the persistence model in production. Consumers must
/// use <see cref="ToMaskedLabel"/> for APIs and logs; only the payout gateway
/// builder is allowed to read the full beneficiary identifier.
/// </summary>
public static class PayoutDestinationSnapshotCodec
{
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string CreateVendorBankAccount(VendorBankAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return Serialize(new PayoutDestinationSnapshot(
            CurrentVersion,
            PayoutDestinationType.VendorBankAccount,
            account.Id,
            account.AccountHolderName,
            account.BankName,
            account.IBAN,
            account.SwiftCode,
            null,
            DateTime.UtcNow));
    }

    public static string CreateDriverPayoutMethod(DriverPayoutMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return Serialize(new PayoutDestinationSnapshot(
            CurrentVersion,
            PayoutDestinationType.DriverPayoutMethod,
            method.Id,
            method.AccountHolderName,
            method.ProviderName,
            method.AccountIdentifier,
            null,
            method.MethodType.ToString(),
            DateTime.UtcNow));
    }

    public static PayoutDestinationSnapshot ParseRequired(Payout payout)
    {
        ArgumentNullException.ThrowIfNull(payout);

        if (!TryParse(payout.DestinationSnapshot, out var snapshot) ||
            snapshot.DestinationType != payout.DestinationType)
        {
            throw new BusinessRuleException(
                "PAYOUT_DESTINATION_SNAPSHOT_REQUIRED",
                "This payout does not have a valid immutable recipient snapshot and must be reviewed before it can be sent.");
        }

        return snapshot;
    }

    public static bool TryParse(string? serialized, out PayoutDestinationSnapshot snapshot)
    {
        snapshot = default!;
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return false;
        }

        try
        {
            var candidate = JsonSerializer.Deserialize<PayoutDestinationSnapshot>(serialized, JsonOptions);
            if (candidate is null ||
                candidate.Version != CurrentVersion ||
                candidate.SourceId == Guid.Empty ||
                string.IsNullOrWhiteSpace(candidate.AccountHolderName) ||
                string.IsNullOrWhiteSpace(candidate.AccountIdentifier) ||
                !Enum.IsDefined(candidate.DestinationType))
            {
                return false;
            }

            snapshot = candidate;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string? ToMaskedLabel(string? serialized)
    {
        if (!TryParse(serialized, out var snapshot))
        {
            return null;
        }

        var identifier = new string(snapshot.AccountIdentifier.Where(char.IsLetterOrDigit).ToArray());
        var lastFour = identifier.Length <= 4 ? identifier : identifier[^4..];
        var prefix = string.IsNullOrWhiteSpace(snapshot.ProviderOrBankName)
            ? snapshot.DestinationType.ToString()
            : snapshot.ProviderOrBankName.Trim();
        return $"{prefix} ****{lastFour}";
    }

    private static string Serialize(PayoutDestinationSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions);
}

/// <summary>
/// Internal data contract persisted only inside the encrypted payout snapshot.
/// Do not return this type directly from an API response.
/// </summary>
public sealed record PayoutDestinationSnapshot(
    int Version,
    PayoutDestinationType DestinationType,
    Guid SourceId,
    string AccountHolderName,
    string? ProviderOrBankName,
    string AccountIdentifier,
    string? SwiftCode,
    string? MethodType,
    DateTime CapturedAtUtc);
