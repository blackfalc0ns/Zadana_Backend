using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Infrastructure.Settings;

namespace Zadana.Infrastructure.Services;

public sealed class PaymobPayoutGateway : IPaymobPayoutGateway
{
    private readonly HttpClient _httpClient;
    private readonly PaymobSettings _settings;
    private readonly ILogger<PaymobPayoutGateway> _logger;

    public PaymobPayoutGateway(
        HttpClient httpClient,
        IOptions<PaymobSettings> settings,
        ILogger<PaymobPayoutGateway> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsEnabled => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public async Task<PaymobPayoutResult> TriggerPayoutAsync(PaymobPayoutRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            api_key = _settings.ApiKey,
            amount_cents = (int)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero),
            currency = request.CurrencyCode,
            reference = request.TransferReference,
            destination_type = request.DestinationType,
            destination = request.DestinationSnapshot,
            metadata = new { payout_id = request.PayoutId }
        };

        var response = await _httpClient.PostAsJsonAsync(_settings.PayoutsEndpoint, payload, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Paymob payout request failed with status {StatusCode}: {Payload}", response.StatusCode, raw);
            return new PaymobPayoutResult(false, null, request.TransferReference, raw, raw);
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        var root = document.RootElement;
        var providerTransferId = TryReadString(root, "id")
            ?? TryReadString(root, "transfer_id")
            ?? TryReadString(root, "transaction_id")
            ?? request.PayoutId.ToString("N");
        var transferReference = TryReadString(root, "reference") ?? request.TransferReference;

        return new PaymobPayoutResult(true, providerTransferId, transferReference, null, raw);
    }

    public PaymobPayoutWebhookNotification ParsePayoutWebhook(string rawPayload)
    {
        using var document = JsonDocument.Parse(rawPayload);
        var root = document.RootElement;

        return new PaymobPayoutWebhookNotification(
            TryReadString(root, "provider_transfer_id")
                ?? TryReadString(root, "transfer_id")
                ?? TryReadString(root, "id")
                ?? string.Empty,
            TryReadString(root, "status") ?? "unknown",
            TryReadString(root, "reference"),
            TryReadString(root, "failure_reason") ?? TryReadString(root, "error"),
            rawPayload);
    }

    private static string? TryReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => property.ToString()
        };
    }
}
