using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;

namespace Zadana.Infrastructure.Services.Payments;

/// <summary>
/// Moyasar implementation of <see cref="IPaymentGateway"/>. Uses the secret
/// key for server-to-server fetch/refund calls; the publishable key is
/// returned to the client through <see cref="CreateSessionAsync"/> and never
/// touches private routes.
/// </summary>
public sealed class MoyasarPaymentGateway : IPaymentGateway
{
    public const string Provider = "Moyasar";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly MoyasarSettings _settings;
    private readonly ILogger<MoyasarPaymentGateway> _logger;

    public MoyasarPaymentGateway(
        HttpClient httpClient,
        IOptions<MoyasarSettings> settings,
        ILogger<MoyasarPaymentGateway> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(_settings.SecretKey + ":"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }
    }

    public string ProviderName => Provider;

    public bool IsEnabled =>
        _settings.Enabled
        && !string.IsNullOrWhiteSpace(_settings.SecretKey)
        && !string.IsNullOrWhiteSpace(_settings.PublishableKey);

    public Task<CreatePaymentSessionResult> CreateSessionAsync(CreatePaymentSessionCommand command, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        CurrencyPolicy.EnsureOfficial(command.Currency);

        // Moyasar's standard form is rendered client-side using the publishable
        // key; no server-side payment is created up front. We hand the SDK the
        // configuration it needs and store metadata for later verification.
        var amountMinor = CurrencyPolicy.ToMinorUnits(command.Amount, command.Currency);

        var metadata = new Dictionary<string, string>(command.Metadata ?? new Dictionary<string, string>())
        {
            ["order_id"] = command.OrderId.ToString(),
            ["payment_id"] = command.PaymentId.ToString(),
        };

        var providerConfig = new
        {
            publishableKey = _settings.PublishableKey,
            amount = amountMinor,
            currency = CurrencyPolicy.OfficialCurrency,
            description = command.Description,
            callbackUrl = string.IsNullOrWhiteSpace(command.CallbackUrl) ? _settings.CallbackUrl : command.CallbackUrl,
            methods = ResolveMethods(command.Channel),
            supportedNetworks = _settings.SupportedNetworks,
            metadata,
        };

        var rawJson = JsonSerializer.Serialize(providerConfig, JsonOpts);

        return Task.FromResult(new CreatePaymentSessionResult(
            ProviderName: Provider,
            ProviderPaymentId: null,
            ProviderInvoiceId: null,
            ClientAction: "RenderMoyasarForm",
            ProviderConfig: providerConfig,
            RawCreateResponse: rawJson));
    }

    public async Task<GatewayPaymentDetails> FetchPaymentAsync(string providerPaymentId, CancellationToken cancellationToken)
    {
        EnsureEnabled();

        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            throw new BusinessRuleException("INVALID_PROVIDER_PAYMENT_ID", "Provider payment id is required.");
        }

        using var response = await _httpClient.GetAsync($"payments/{providerPaymentId}", cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Moyasar fetch payment {Id} failed: {Status} {Body}", providerPaymentId, response.StatusCode, raw);
            throw new ExternalServiceException("MOYASAR_FETCH_FAILED", $"Moyasar fetch failed: {response.StatusCode}");
        }

        return ParsePayment(raw);
    }

    public async Task<RefundGatewayResult> RefundAsync(RefundGatewayCommand command, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        CurrencyPolicy.EnsureOfficial(command.Currency);

        var minor = CurrencyPolicy.ToMinorUnits(command.Amount, command.Currency);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["amount"] = minor.ToString(CultureInfo.InvariantCulture),
        });

        using var response = await _httpClient.PostAsync($"payments/{command.ProviderPaymentId}/refund", content, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Moyasar refund for payment {Id} failed: {Status} {Body}", command.ProviderPaymentId, response.StatusCode, raw);
            return new RefundGatewayResult(
                ProviderName: Provider,
                ProviderRefundId: null,
                ProviderStatus: "failed",
                Amount: command.Amount,
                Currency: command.Currency,
                FailureMessage: $"Moyasar refund failed: {response.StatusCode}",
                RawResponse: raw);
        }

        var parsed = ParsePayment(raw);
        return new RefundGatewayResult(
            ProviderName: Provider,
            ProviderRefundId: parsed.ProviderPaymentId,
            ProviderStatus: parsed.ProviderStatus,
            Amount: command.Amount,
            Currency: command.Currency,
            FailureMessage: null,
            RawResponse: raw);
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled)
        {
            throw new BusinessRuleException("PAYMENT_UNAVAILABLE", "Moyasar gateway is disabled or missing required configuration.");
        }
    }

    private string[] ResolveMethods(PaymentMethodChannel channel)
    {
        return channel switch
        {
            PaymentMethodChannel.ApplePay => ["applepay"],
            PaymentMethodChannel.SamsungPay => ["samsungpay"],
            PaymentMethodChannel.StcPay => ["stcpay"],
            PaymentMethodChannel.Card => ["creditcard"],
            _ => _settings.EnabledMethods.Length > 0 ? _settings.EnabledMethods : ["creditcard"],
        };
    }

    private static GatewayPaymentDetails ParsePayment(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var id = ReadString(root, "id") ?? string.Empty;
        var status = ReadString(root, "status") ?? "unknown";
        var amount = ReadInt64(root, "amount") ?? 0L;
        var currency = ReadString(root, "currency") ?? CurrencyPolicy.OfficialCurrency;
        var invoiceId = ReadString(root, "invoice_id");
        var rrn = ReadString(root, "source", "reference_number") ?? ReadString(root, "rrn");
        var capturedAt = ReadDateTime(root, "captured_at");
        var failureMessage = ReadString(root, "source", "message") ?? ReadString(root, "message");

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in meta.EnumerateObject())
            {
                metadata[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    _ => prop.Value.GetRawText(),
                };
            }
        }

        return new GatewayPaymentDetails(
            ProviderName: Provider,
            ProviderPaymentId: id,
            ProviderStatus: status,
            AmountMinorUnits: amount,
            Currency: currency.ToUpperInvariant(),
            Metadata: metadata,
            ProviderInvoiceId: invoiceId,
            ProviderReferenceNumber: rrn,
            CapturedAtUtc: capturedAt,
            RawResponse: raw,
            FailureMessage: failureMessage);
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
