using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Finances.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;

namespace Zadana.Application.Modules.Finances.Services;

public sealed class PayoutOrchestrator
{
    private readonly IApplicationDbContext _context;
    private readonly IEnumerable<IPayoutGateway> _payoutGateways;
    private readonly FinancialEventPostingService _postingService;
    private readonly WalletProjectionUpdater _walletProjectionUpdater;
    private readonly VendorPayoutWalletService _vendorPayoutWalletService;
    private readonly FinancialSettingsOptions _settings;
    private readonly IAdminAlertService _adminAlertService;
    private readonly INotificationService _notificationService;
    private readonly IOneSignalPushService _oneSignalPushService;

    public PayoutOrchestrator(
        IApplicationDbContext context,
        IEnumerable<IPayoutGateway> payoutGateways,
        FinancialEventPostingService postingService,
        WalletProjectionUpdater walletProjectionUpdater,
        VendorPayoutWalletService vendorPayoutWalletService,
        IOptions<FinancialSettingsOptions> settings,
        IAdminAlertService adminAlertService,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService)
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
    }

    public bool HasEnabledGateway => GetEnabledGateway() is not null;

    public async Task<Payout> TriggerAsync(Guid payoutId, Guid? processedByUserId = null, bool isRetry = false, CancellationToken cancellationToken = default)
    {
        var payout = await LoadPayoutAsync(payoutId, cancellationToken);

        if (payout.Status == PayoutStatus.Paid ||
            (payout.Status == PayoutStatus.Cancelled && !isRetry))
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_CLOSED", "Closed payouts cannot be triggered.");
        }

        if (!isRetry && payout.Status is PayoutStatus.Queued or PayoutStatus.Processing)
        {
            return payout;
        }

        EnsureSettlementCanBeTriggered(payout);

        var gateway = GetEnabledGateway();

        if (gateway is null)
        {
            payout.MarkAsProcessing();
            payout.Settlement.MarkAsProcessing();
            await MarkLinkedDriverWithdrawalProcessingAsync(payout.Id, cancellationToken);
            payout.MarkQueued(providerName: "Manual");
            _context.PayoutAttempts.Add(new PayoutAttempt(
                payout.Id,
                isRetry ? PayoutAttemptType.Retry : PayoutAttemptType.Trigger,
                PayoutStatus.Queued,
                providerName: "Manual",
                providerTransferId: null,
                transferReference: payout.TransferReference,
                failureReason: null,
                rawPayload: null));
            await _context.SaveChangesAsync(cancellationToken);
            return payout;
        }

        CreatePayoutCommand? command = null;
        var providerSubmitAttempted = false;

        try
        {
            command = await BuildGatewayCommandAsync(payout, cancellationToken);
            ValidateGatewayCommand(command);

            payout.MarkAsProcessing(
                providerName: gateway.ProviderName,
                providerSequenceNumber: command.SequenceNumber);
            payout.Settlement.MarkAsProcessing();
            await MarkLinkedDriverWithdrawalProcessingAsync(payout.Id, cancellationToken);

            _context.PayoutAttempts.Add(new PayoutAttempt(
                payout.Id,
                isRetry ? PayoutAttemptType.Retry : PayoutAttemptType.Trigger,
                PayoutStatus.Processing,
                providerName: gateway.ProviderName,
                transferReference: payout.TransferReference));

            // Persist the sequence before the external POST. If the process dies
            // after Moyasar accepts the request, retries reuse the same sequence.
            await _context.SaveChangesAsync(cancellationToken);

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

            payout.MarkAsFailed(ex.Message);
            payout.Settlement.MarkPayoutFailed();
            await ReleaseVendorHoldIfApplicableAsync(payout, ex.Message, cancellationToken);
            await MarkLinkedDriverWithdrawalFailedAsync(payout.Id, ex.Message, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await SendPayoutIntegrationFailureAlertAsync(payout, ex, cancellationToken);
            throw;
        }
    }

    public async Task<Payout> RefreshStatusAsync(Guid payoutId, CancellationToken cancellationToken = default)
    {
        var payout = await LoadPayoutAsync(payoutId, cancellationToken);

        if (payout.Status is PayoutStatus.Paid or PayoutStatus.Cancelled)
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
            .FirstOrDefaultAsync(
                item =>
                    (providerTransferId != null && item.ProviderTransferId == providerTransferId) ||
                    (providerSequenceNumber != null && item.ProviderSequenceNumber == providerSequenceNumber),
                cancellationToken);

        if (payout is null)
        {
            return null;
        }

        if (payout.Status is PayoutStatus.Paid or PayoutStatus.Cancelled)
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

    public async Task<Payout> MarkPaidAsync(
        Guid payoutId,
        string transferReference,
        string? providerTransferId = null,
        CancellationToken cancellationToken = default)
    {
        var payout = await LoadPayoutAsync(payoutId, cancellationToken);

        if (payout.Status == PayoutStatus.Paid)
        {
            return payout;
        }

        await MarkPaidCoreAsync(
            payout,
            transferReference,
            providerTransferId ?? payout.ProviderTransferId,
            payout.ProviderName,
            payout.ProviderSequenceNumber,
            cancellationToken);

        return payout;
    }

    public async Task CancelAsync(Guid payoutId, CancellationToken cancellationToken = default)
    {
        var payout = await LoadPayoutAsync(payoutId, cancellationToken);

        if (payout.Status == PayoutStatus.Paid)
        {
            throw new BusinessRuleException("PAYOUT_ALREADY_PAID", "Paid payouts cannot be cancelled.");
        }

        payout.Cancel();
        payout.Settlement.Hold();
        await CancelLinkedDriverWithdrawalAsync(payout.Id, "Payout cancelled.", cancellationToken);
        _context.PayoutAttempts.Add(new PayoutAttempt(payout.Id, PayoutAttemptType.Cancel, PayoutStatus.Cancelled));
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Payout> LoadPayoutAsync(Guid payoutId, CancellationToken cancellationToken)
    {
        return await _context.Payouts
            .Include(item => item.Settlement)
            .Include(item => item.VendorBankAccount)
            .FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken)
            ?? throw new NotFoundException("Payout", payoutId);
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
        var bankAccount = payout.VendorBankAccount ??
            (payout.VendorBankAccountId.HasValue
                ? await _context.VendorBankAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == payout.VendorBankAccountId.Value, cancellationToken)
                : null)
            ?? throw new BusinessRuleException("VENDOR_BANK_ACCOUNT_REQUIRED", "Vendor bank account is required before sending payout.");

        var vendor = await _context.Vendors
            .AsNoTracking()
            .Where(item => item.Id == payout.Settlement.OwnerId)
            .Select(item => new { item.ContactPhone, item.City })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Vendor", payout.Settlement.OwnerId);

        return BuildCommand(
            payout,
            bankAccount.AccountHolderName,
            bankAccount.IBAN,
            bankAccount.BankName,
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
        var withdrawal = await _context.DriverWithdrawalRequests
            .AsNoTracking()
            .Include(item => item.DriverPayoutMethod)
            .FirstOrDefaultAsync(item => item.PayoutId == payout.Id, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_WITHDRAWAL_REQUIRED", "Driver payout must be linked to a withdrawal request.");

        if (withdrawal.DriverPayoutMethod.MethodType != DriverPayoutMethodType.BankAccount)
        {
            throw new BusinessRuleException("DRIVER_BANK_ACCOUNT_REQUIRED", "Only bank account withdrawal methods can be paid through Moyasar payouts.");
        }

        var driver = await _context.Drivers
            .AsNoTracking()
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == withdrawal.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", withdrawal.DriverId);

        return BuildCommand(
            payout,
            withdrawal.DriverPayoutMethod.AccountHolderName,
            withdrawal.DriverPayoutMethod.AccountIdentifier,
            withdrawal.DriverPayoutMethod.ProviderName,
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
        payout.MarkAsPaid(transferReference, providerTransferId, providerName, providerSequenceNumber);
        payout.Settlement.MarkPaidOut();
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

        // Notify driver or vendor that their settlement/payout is complete
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
        payout.MarkAsFailed(failureReason, providerTransferId, providerName, providerSequenceNumber);
        payout.Settlement.MarkPayoutFailed();
        await ReleaseVendorHoldIfApplicableAsync(payout, failureReason, cancellationToken);
        await MarkLinkedDriverWithdrawalFailedAsync(payout.Id, failureReason, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
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

        throw new BusinessRuleException(
            "SETTLEMENT_APPROVAL_REQUIRED",
            "Scheduled and manual settlements must be approved by finance before payout can be triggered.");
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
            $"payout-paid:{payout.Id:N}:{payout.ProviderTransferId ?? payout.ProviderSequenceNumber ?? payout.TransferReference}",
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
                "/finances/payouts",
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

                var data = $"{{\"payoutId\":\"{payout.Id}\",\"settlementId\":\"{payout.SettlementId}\",\"amount\":{payout.Amount},\"transferReference\":\"{payout.TransferReference}\",\"targetUrl\":\"/wallet\"}}";

                await _notificationService.SendToUserAsync(
                    driverUserId,
                    "تم إتمام التحويل إلى حسابك البنكي",
                    "Payout completed",
                    $"تم تحويل مبلغ {payout.Amount:0.00} ريال إلى حسابك البنكي بنجاح.",
                    $"A payout of {payout.Amount:0.00} SAR has been successfully transferred to your bank account.",
                    NotificationTypes.DriverWalletUpdated,
                    payout.Id,
                    data,
                    cancellationToken);

                await _notificationService.SendDriverWalletUpdatedAsync(driverUserId, cancellationToken);

                await _oneSignalPushService.SendToExternalUserAsync(
                    driverUserId.ToString(),
                    "تم إتمام التحويل إلى حسابك البنكي",
                    "Payout completed",
                    $"تم تحويل مبلغ {payout.Amount:0.00} ريال إلى حسابك البنكي بنجاح.",
                    $"A payout of {payout.Amount:0.00} SAR has been successfully transferred to your bank account.",
                    NotificationTypes.DriverWalletUpdated,
                    payout.Id,
                    data,
                    "/wallet",
                    OneSignalPushProfile.MobileStandard,
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
                    "تم صرف مستحقاتك",
                    "Settlement paid",
                    $"تم تحويل مستحقاتك بمبلغ {payout.Amount:0.00} ريال إلى حسابك البنكي.",
                    $"Your settlement of {payout.Amount:0.00} SAR has been transferred to your bank account.",
                    NotificationTypes.VendorSettlementPaid,
                    payout.Id,
                    data,
                    cancellationToken);

                await _oneSignalPushService.SendToExternalUserAsync(
                    vendorUserId.ToString(),
                    "تم صرف مستحقاتك",
                    "Settlement paid",
                    $"تم تحويل مستحقاتك بمبلغ {payout.Amount:0.00} ريال إلى حسابك البنكي.",
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
