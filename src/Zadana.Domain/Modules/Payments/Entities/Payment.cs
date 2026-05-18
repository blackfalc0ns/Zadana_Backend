using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Payments.Entities;

public class Payment : BaseEntity
{
    public Guid OrderId { get; private set; }
    public PaymentMethodType Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? ProviderName { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public string? CheckoutDeviceId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }

    /// <summary>Provider-specific channel name (e.g. "creditcard", "applepay", "stcpay"). Null for legacy rows.</summary>
    public string? ProviderMethod { get; private set; }

    /// <summary>Optional invoice id returned by the provider (used by Moyasar/STC Pay flows).</summary>
    public string? ProviderInvoiceId { get; private set; }

    /// <summary>Last-known status string from the provider (e.g. "paid", "captured", "authorized").</summary>
    public string? ProviderStatus { get; private set; }

    /// <summary>Provider reference (RRN / receipt number).</summary>
    public string? ProviderReferenceNumber { get; private set; }

    /// <summary>Currency the customer was charged in. SAR for new payments.</summary>
    public string Currency { get; private set; } = "SAR";

    /// <summary>Idempotency key for the create-session call. Unique when not null.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Raw JSON returned by the provider when creating the session.</summary>
    public string? RawCreateResponse { get; private set; }

    /// <summary>Raw JSON of the most recent provider fetch.</summary>
    public string? RawFetchResponse { get; private set; }

    // Navigation
    public Order Order { get; private set; } = null!;
    public ICollection<Refund> Refunds { get; private set; } = [];

    private Payment() { }

    public Payment(Guid orderId, PaymentMethodType method, decimal amount)
    {
        if (amount < 0) throw new BusinessRuleException("INVALID_AMOUNT", "Payment amount cannot be negative.");

        OrderId = orderId;
        Method = method;
        Amount = amount;
        Status = PaymentStatus.Initiated;
        Currency = "SAR";
    }

    public void ApplyProviderSession(
        string providerName,
        string? providerMethod,
        string? providerPaymentId,
        string? providerInvoiceId,
        string idempotencyKey,
        string? rawCreateResponse,
        string? currency = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new BusinessRuleException("INVALID_PROVIDER_NAME", "Provider name is required.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessRuleException("INVALID_IDEMPOTENCY_KEY", "Idempotency key is required.");
        }

        ProviderName = providerName.Trim();
        ProviderMethod = string.IsNullOrWhiteSpace(providerMethod) ? null : providerMethod.Trim();
        ProviderTransactionId = string.IsNullOrWhiteSpace(providerPaymentId) ? ProviderTransactionId : providerPaymentId.Trim();
        ProviderInvoiceId = string.IsNullOrWhiteSpace(providerInvoiceId) ? null : providerInvoiceId.Trim();
        IdempotencyKey = idempotencyKey.Trim();
        RawCreateResponse = rawCreateResponse;
        if (!string.IsNullOrWhiteSpace(currency))
        {
            Currency = currency.Trim().ToUpperInvariant();
        }
        Status = PaymentStatus.Pending;
        Order?.UpdatePaymentStatus(Status);
    }

    public void ApplyProviderFetch(
        string providerStatus,
        string? providerReferenceNumber,
        string? rawFetchResponse)
    {
        ProviderStatus = string.IsNullOrWhiteSpace(providerStatus) ? null : providerStatus.Trim();
        ProviderReferenceNumber = string.IsNullOrWhiteSpace(providerReferenceNumber) ? ProviderReferenceNumber : providerReferenceNumber.Trim();
        RawFetchResponse = rawFetchResponse;
    }

    public void MarkAsPending(string providerName, string transactionId)
    {
        ProviderName = providerName.Trim();
        ProviderTransactionId = transactionId.Trim();
        Status = PaymentStatus.Pending;
        FailedAtUtc = null;
        Order?.UpdatePaymentStatus(Status);
    }

    public void SetProviderTransactionId(string transactionId)
    {
        ProviderTransactionId = transactionId.Trim();
    }

    public void SetCheckoutDeviceId(string? deviceId)
    {
        CheckoutDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
    }

    public void MarkAsPaid(string? transactionId = null)
    {
        if (!string.IsNullOrWhiteSpace(transactionId))
        {
            ProviderTransactionId = transactionId.Trim();
        }

        Status = PaymentStatus.Paid;
        PaidAtUtc = DateTime.UtcNow;
        FailedAtUtc = null;
        Order?.UpdatePaymentStatus(Status);
    }

    public void MarkAsFailed(string? failureReason, string? transactionId = null)
    {
        if (!string.IsNullOrWhiteSpace(transactionId))
        {
            ProviderTransactionId = transactionId.Trim();
        }

        Status = PaymentStatus.Failed;
        FailedAtUtc = DateTime.UtcNow;
        Order?.UpdatePaymentStatus(Status);
    }
}
