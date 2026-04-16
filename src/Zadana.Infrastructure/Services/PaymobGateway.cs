using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Infrastructure.Services;

public class PaymobGateway : IPaymobGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymobGateway> _logger;
    private readonly PaymobSettings _settings;

    public PaymobGateway(
        HttpClient httpClient,
        IOptions<PaymobSettings> settings,
        ILogger<PaymobGateway> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsEnabled =>
        _settings.Enabled &&
        !string.IsNullOrWhiteSpace(_settings.ApiKey) &&
        _settings.IframeId > 0 &&
        _settings.IntegrationId > 0;

    public async Task<PaymobCheckoutSessionDto> CreateCheckoutSessionAsync(
        PaymobCheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var authToken = await AuthenticateAsync(cancellationToken);
        var amountCents = ToAmountCents(request.Amount);
        var providerOrderId = await CreateOrderAsync(authToken, request, amountCents, cancellationToken);
        var paymentToken = await CreatePaymentKeyAsync(authToken, providerOrderId, request, amountCents, cancellationToken);
        var iframeUrl = BuildIframeUrl(paymentToken);

        return new PaymobCheckoutSessionDto(providerOrderId, paymentToken, iframeUrl);
    }

    public PaymobWebhookNotificationDto ParseWebhookNotification(string rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            throw new BadRequestException("INVALID_PAYLOAD", "Webhook payload is required.");
        }

        using var document = JsonDocument.Parse(rawPayload);
        var root = document.RootElement;
        var transaction = TryGetProperty(root, "obj") ?? root;

        if (!string.IsNullOrWhiteSpace(_settings.HmacSecret))
        {
            var providedHmac = GetString(root, "hmac");
            if (string.IsNullOrWhiteSpace(providedHmac))
            {
                throw new BadRequestException("INVALID_PAYMOB_HMAC", "Missing Paymob webhook signature.");
            }

            var expectedHmac = ComputeHmac(transaction, _settings.HmacSecret);
            if (!string.Equals(providedHmac, expectedHmac, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("INVALID_PAYMOB_HMAC", "Invalid Paymob webhook signature.");
            }
        }

        var order = TryGetProperty(transaction, "order");
        var merchantOrderId = GetString(order, "merchant_order_id") ?? GetString(root, "merchant_order_id");
        Guid? paymentId = Guid.TryParse(merchantOrderId, out var parsedPaymentId) ? parsedPaymentId : null;

        var providerReference = GetString(order, "id");
        var providerTransactionId = GetString(transaction, "id");
        var success = GetBool(transaction, "success");
        var pending = GetBool(transaction, "pending");
        var errorOccured = GetBool(transaction, "error_occured");

        return new PaymobWebhookNotificationDto(
            paymentId,
            providerReference,
            providerTransactionId,
            success && !pending && !errorOccured,
            pending,
            GetString(root, "type") ?? "TRANSACTION");
    }

    private async Task<string> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/auth/tokens",
            new { api_key = _settings.ApiKey },
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, payload, "PAYMOB_AUTH_FAILED");

        using var document = JsonDocument.Parse(payload);
        return GetRequiredString(document.RootElement, "token", "PAYMOB_AUTH_FAILED");
    }

    private async Task<string> CreateOrderAsync(
        string authToken,
        PaymobCheckoutSessionRequest request,
        int amountCents,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/ecommerce/orders",
            new
            {
                auth_token = authToken,
                delivery_needed = false,
                amount_cents = amountCents.ToString(CultureInfo.InvariantCulture),
                currency = request.Currency,
                merchant_order_id = request.PaymentId.ToString("D"),
                items = Array.Empty<object>()
            },
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, payload, "PAYMOB_ORDER_FAILED");

        using var document = JsonDocument.Parse(payload);
        return GetRequiredString(document.RootElement, "id", "PAYMOB_ORDER_FAILED");
    }

    private async Task<string> CreatePaymentKeyAsync(
        string authToken,
        string providerOrderId,
        PaymobCheckoutSessionRequest request,
        int amountCents,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/acceptance/payment_keys",
            new
            {
                auth_token = authToken,
                amount_cents = amountCents.ToString(CultureInfo.InvariantCulture),
                expiration = _settings.PaymentKeyExpirationSeconds,
                order_id = providerOrderId,
                billing_data = new
                {
                    apartment = "NA",
                    email = RequiredBillingValue(request.CustomerEmail, "email"),
                    floor = "NA",
                    first_name = RequiredBillingValue(request.CustomerFirstName, "first_name"),
                    street = RequiredBillingValue(request.AddressLine, "street"),
                    building = "NA",
                    phone_number = RequiredBillingValue(request.CustomerPhone, "phone_number"),
                    shipping_method = "PKG",
                    postal_code = "NA",
                    city = RequiredBillingValue(request.City, "city"),
                    country = RequiredBillingValue(request.CountryCode, "country"),
                    last_name = RequiredBillingValue(request.CustomerLastName, "last_name"),
                    state = request.City
                },
                currency = request.Currency,
                integration_id = _settings.IntegrationId,
                lock_order_when_paid = true
            },
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, payload, "PAYMOB_PAYMENT_KEY_FAILED");

        using var document = JsonDocument.Parse(payload);
        return GetRequiredString(document.RootElement, "token", "PAYMOB_PAYMENT_KEY_FAILED");
    }

    private string BuildIframeUrl(string paymentToken) =>
        $"{_settings.BaseUrl.TrimEnd('/')}/api/acceptance/iframes/{_settings.IframeId}?payment_token={Uri.EscapeDataString(paymentToken)}";

    private void EnsureConfigured()
    {
        if (!IsEnabled)
        {
            throw new BusinessRuleException("PAYMENT_UNAVAILABLE", "Paymob checkout is disabled or missing required settings.");
        }
    }

    private void EnsureSuccess(HttpResponseMessage response, string payload, string errorCode)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        _logger.LogError("Paymob request failed with status {StatusCode}. Payload: {Payload}", response.StatusCode, payload);
        throw new ExternalServiceException(errorCode, $"Paymob request failed. Provider response: {payload}");
    }

    private static int ToAmountCents(decimal amount) =>
        (int)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    private static string RequiredBillingValue(string? value, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessRuleException("INVALID_BILLING_DATA", $"Missing billing field required by Paymob: {fieldName}.");
        }

        return normalized;
    }

    private static string ComputeHmac(JsonElement transaction, string secret)
    {
        var order = TryGetProperty(transaction, "order");
        var sourceData = TryGetProperty(transaction, "source_data");

        var concatenated =
            GetText(transaction, "amount_cents") +
            GetText(transaction, "created_at") +
            GetText(transaction, "currency") +
            GetText(transaction, "error_occured") +
            GetText(transaction, "has_parent_transaction") +
            GetText(transaction, "id") +
            GetText(transaction, "integration_id") +
            GetText(transaction, "is_3d_secure") +
            GetText(transaction, "is_auth") +
            GetText(transaction, "is_capture") +
            GetText(transaction, "is_refunded") +
            GetText(transaction, "is_standalone_payment") +
            GetText(transaction, "is_voided") +
            GetText(order, "id") +
            GetText(transaction, "owner") +
            GetText(transaction, "pending") +
            GetText(sourceData, "pan") +
            GetText(sourceData, "sub_type") +
            GetText(sourceData, "type") +
            GetText(transaction, "success");

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(concatenated);
        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetRequiredString(JsonElement element, string propertyName, string errorCode)
    {
        var value = GetString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ExternalServiceException(errorCode, $"Paymob response did not contain '{propertyName}'.");
        }

        return value;
    }

    private static JsonElement? TryGetProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return element.TryGetProperty(propertyName, out var value) ? value : null;
    }

    private static string? GetString(JsonElement? element, string propertyName)
    {
        if (!element.HasValue)
        {
            return null;
        }

        if (!element.Value.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            _ => value.GetRawText().Trim('"')
        };
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false
        };
    }

    private static string GetText(JsonElement? element, string propertyName) =>
        GetString(element, propertyName) ?? string.Empty;
}
