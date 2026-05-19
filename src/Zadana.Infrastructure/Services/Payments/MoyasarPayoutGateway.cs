using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;

namespace Zadana.Infrastructure.Services.Payments;

public sealed class MoyasarPayoutGateway : IPayoutGateway
{
    public const string Provider = "Moyasar";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IApplicationDbContext? _context;
    private readonly MoyasarSettings _settings;
    private readonly ILogger<MoyasarPayoutGateway> _logger;

    public MoyasarPayoutGateway(
        HttpClient httpClient,
        IOptions<MoyasarSettings> settings,
        ILogger<MoyasarPayoutGateway> logger,
        IApplicationDbContext? context = null)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _context = context;

        if (!string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(_settings.SecretKey + ":"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }
    }

    public string ProviderName => Provider;

    public bool IsEnabled
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_settings.SecretKey))
            {
                return false;
            }

            if (_settings.Payouts.Enabled && !string.IsNullOrWhiteSpace(_settings.Payouts.SourceId))
            {
                return true;
            }

            return _context is not null &&
                _context.PlatformBankAccounts
                    .AsNoTracking()
                    .Any(item =>
                        item.IsActive &&
                        item.IsMoyasarPayoutsEnabled &&
                        item.MoyasarPayoutSourceId != null &&
                        item.MoyasarPayoutSourceId != string.Empty);
        }
    }

    public async Task<PayoutGatewayResult> CreatePayoutAsync(CreatePayoutCommand command, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        CurrencyPolicy.EnsureOfficial(command.Currency);

        if (string.IsNullOrWhiteSpace(command.BeneficiaryIban))
        {
            throw new BusinessRuleException("PAYOUT_BENEFICIARY_IBAN_REQUIRED", "Beneficiary IBAN is required.");
        }

        if (string.IsNullOrWhiteSpace(command.BeneficiaryName))
        {
            throw new BusinessRuleException("PAYOUT_BENEFICIARY_NAME_REQUIRED", "Beneficiary name is required.");
        }

        if (string.IsNullOrWhiteSpace(command.BeneficiaryMobile))
        {
            throw new BusinessRuleException("PAYOUT_BENEFICIARY_MOBILE_REQUIRED", "Beneficiary mobile is required.");
        }

        var beneficiaryIban = NormalizeIban(command.BeneficiaryIban);
        var country = NormalizeOrDefault(command.BeneficiaryCountry, _settings.Payouts.DefaultCountry).ToUpperInvariant();
        if (country == "SA" && !IsSaudiIban(beneficiaryIban))
        {
            throw new BusinessRuleException("PAYOUT_BENEFICIARY_IBAN_INVALID", "Beneficiary IBAN must be a valid Saudi IBAN.");
        }

        var sequenceNumber = string.IsNullOrWhiteSpace(command.SequenceNumber)
            ? BuildSequenceNumber(command.PayoutId)
            : command.SequenceNumber.Trim();
        var sourceId = await ResolvePayoutSourceIdAsync(cancellationToken);

        var payload = new
        {
            source_id = sourceId,
            sequence_number = sequenceNumber,
            amount = CurrencyPolicy.ToMinorUnits(command.Amount, command.Currency),
            purpose = ResolvePurpose(command),
            destination = new
            {
                type = "bank",
                iban = beneficiaryIban,
                name = command.BeneficiaryName.Trim(),
                mobile = command.BeneficiaryMobile.Trim(),
                country,
                city = NormalizeOrDefault(command.BeneficiaryCity, _settings.Payouts.DefaultCity)
            },
            comment = string.IsNullOrWhiteSpace(command.Comment) ? command.Reference : command.Comment,
            metadata = command.Metadata
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("payouts", content, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Moyasar payout {PayoutId} timed out after submit. Sequence {SequenceNumber}", command.PayoutId, sequenceNumber);
            return UnknownResult(sequenceNumber, "Moyasar payout request timed out after submit.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Moyasar payout {PayoutId} transport failure after submit. Sequence {SequenceNumber}", command.PayoutId, sequenceNumber);
            return UnknownResult(sequenceNumber, ex.Message);
        }

        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Moyasar payout {PayoutId} failed: {Status} {Body}", command.PayoutId, response.StatusCode, raw);
                if (IsTransientStatus(response.StatusCode))
                {
                    return new PayoutGatewayResult(
                        Provider,
                        ProviderTransferId: null,
                        ProviderStatus: "unknown",
                        FailureMessage: ExtractFailure(raw) ?? $"Moyasar payout status is unknown after {response.StatusCode}",
                        RawResponse: raw,
                        ProviderSequenceNumber: sequenceNumber,
                        IsTransient: true);
                }

                return new PayoutGatewayResult(
                    Provider,
                    ProviderTransferId: null,
                    ProviderStatus: "failed",
                    FailureMessage: ExtractFailure(raw) ?? $"Moyasar payout failed: {response.StatusCode}",
                    RawResponse: raw,
                    ProviderSequenceNumber: sequenceNumber);
            }

            var parsed = ParseDetails(raw);
            return new PayoutGatewayResult(
                Provider,
                parsed.ProviderTransferId,
                parsed.ProviderStatus,
                parsed.FailureMessage,
                raw,
                parsed.ProviderSequenceNumber ?? sequenceNumber);
        }
    }

    public async Task<PayoutGatewayDetails> FetchPayoutAsync(string providerTransferId, CancellationToken cancellationToken)
    {
        EnsureEnabled();

        if (string.IsNullOrWhiteSpace(providerTransferId))
        {
            throw new BusinessRuleException("INVALID_PROVIDER_TRANSFER_ID", "Provider transfer id is required.");
        }

        using var response = await _httpClient.GetAsync($"payout/{providerTransferId.Trim()}", cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Moyasar fetch payout {Id} failed: {Status} {Body}", providerTransferId, response.StatusCode, raw);
            throw new ExternalServiceException("MOYASAR_PAYOUT_FETCH_FAILED", $"Moyasar payout fetch failed: {response.StatusCode}");
        }

        return ParseDetails(raw);
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled)
        {
            throw new BusinessRuleException("PAYOUT_GATEWAY_UNAVAILABLE", "Moyasar payouts are missing required secret key configuration.");
        }
    }

    private async Task<string> ResolvePayoutSourceIdAsync(CancellationToken cancellationToken)
    {
        if (_context is not null)
        {
            var platformSourceId = await _context.PlatformBankAccounts
                .AsNoTracking()
                .Where(item => item.IsActive && item.IsMoyasarPayoutsEnabled && item.MoyasarPayoutSourceId != null && item.MoyasarPayoutSourceId != "")
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ThenByDescending(item => item.CreatedAtUtc)
                .Select(item => item.MoyasarPayoutSourceId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(platformSourceId))
            {
                return platformSourceId.Trim();
            }
        }

        if (_settings.Payouts.Enabled && !string.IsNullOrWhiteSpace(_settings.Payouts.SourceId))
        {
            return _settings.Payouts.SourceId.Trim();
        }

        throw new BusinessRuleException(
            "MOYASAR_PAYOUT_SOURCE_REQUIRED",
            "Moyasar payout source id is required. Configure the platform payout account first.");
    }

    private string ResolvePurpose(CreatePayoutCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.Purpose))
        {
            return command.Purpose.Trim();
        }

        return string.Equals(command.OwnerType, "Driver", StringComparison.OrdinalIgnoreCase)
            ? _settings.Payouts.DriverPurpose
            : _settings.Payouts.VendorPurpose;
    }

    private static PayoutGatewayDetails ParseDetails(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var id = ReadString(root, "id") ?? string.Empty;
        var status = ReadString(root, "status") ?? "unknown";
        var amount = ReadInt64(root, "amount") ?? 0L;
        var currency = ReadString(root, "currency") ?? CurrencyPolicy.OfficialCurrency;
        var sequenceNumber = ReadString(root, "sequence_number");
        var failureMessage = ReadString(root, "failure_reason") ?? ReadString(root, "message");
        var updatedAt = ReadDateTime(root, "updated_at");

        return new PayoutGatewayDetails(
            Provider,
            id,
            status,
            CurrencyPolicy.FromMinorUnits(amount, currency),
            currency.ToUpperInvariant(),
            CompletedAtUtc: string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase) ? updatedAt : null,
            FailureMessage: string.IsNullOrWhiteSpace(failureMessage) ? null : failureMessage,
            RawResponse: raw,
            ProviderSequenceNumber: sequenceNumber);
    }

    private static string? ExtractFailure(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return ReadString(doc.RootElement, "message") ?? ReadString(doc.RootElement, "failure_reason");
        }
        catch (JsonException)
        {
            return raw;
        }
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

    private static PayoutGatewayResult UnknownResult(string sequenceNumber, string reason) =>
        new(
            Provider,
            ProviderTransferId: null,
            ProviderStatus: "unknown",
            FailureMessage: reason,
            RawResponse: null,
            ProviderSequenceNumber: sequenceNumber,
            IsTransient: true);

    private static bool IsTransientStatus(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == HttpStatusCode.Conflict ||
               statusCode == HttpStatusCode.TooManyRequests ||
               code >= 500;
    }

    private static string NormalizeIban(string iban) =>
        new string(iban.Where(ch => !char.IsWhiteSpace(ch)).ToArray()).ToUpperInvariant();

    private static bool IsSaudiIban(string iban) =>
        iban.Length == 24 &&
        iban.StartsWith("SA", StringComparison.OrdinalIgnoreCase) &&
        iban.Skip(2).All(char.IsDigit);

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        var resolved = string.IsNullOrWhiteSpace(value) ? fallback : value;
        return resolved.Trim();
    }

    private static string? ReadString(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object) return null;
            if (!current.TryGetProperty(segment, out var next)) return null;
            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => current.GetRawText(),
        };
    }

    private static long? ReadInt64(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var l) => l,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    private static DateTime? ReadDateTime(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) return null;
        return DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
            ? dt
            : null;
    }
}
