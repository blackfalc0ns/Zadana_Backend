using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Payments.Entities;

/// <summary>
/// Durable inbox for callbacks/webhooks coming from a payment provider.
/// Per section 7.6 of the revised spec, the controller writes here first
/// (with a unique constraint on Provider + ProviderEventId) and returns 2xx
/// quickly. A background worker performs the actual fetch + verification.
/// </summary>
public class PaymentProviderEventInbox : BaseEntity
{
    public string ProviderName { get; private set; } = null!;
    public string ProviderEventId { get; private set; } = null!;
    public string EventType { get; private set; } = null!;
    public string? ProviderPaymentId { get; private set; }

    public bool SecretValid { get; private set; }
    public string RawPayload { get; private set; } = null!;
    public string? Headers { get; private set; }

    public PaymentProviderEventStatus Status { get; private set; }
    public string? FailureReason { get; private set; }

    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? ProcessingStartedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    public int ProcessingAttempts { get; private set; }

    private PaymentProviderEventInbox() { }

    public PaymentProviderEventInbox(
        string providerName,
        string providerEventId,
        string eventType,
        string rawPayload,
        bool secretValid,
        string? providerPaymentId = null,
        string? headers = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new BusinessRuleException("INVALID_PROVIDER_NAME", "Provider name is required.");
        }

        if (string.IsNullOrWhiteSpace(providerEventId))
        {
            throw new BusinessRuleException("INVALID_PROVIDER_EVENT_ID", "Provider event id is required.");
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new BusinessRuleException("INVALID_EVENT_TYPE", "Event type is required.");
        }

        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            throw new BusinessRuleException("INVALID_PAYLOAD", "Raw payload is required.");
        }

        ProviderName = providerName.Trim();
        ProviderEventId = providerEventId.Trim();
        EventType = eventType.Trim();
        ProviderPaymentId = string.IsNullOrWhiteSpace(providerPaymentId) ? null : providerPaymentId.Trim();
        RawPayload = rawPayload;
        Headers = string.IsNullOrWhiteSpace(headers) ? null : headers;
        SecretValid = secretValid;
        Status = secretValid ? PaymentProviderEventStatus.Received : PaymentProviderEventStatus.Failed;
        FailureReason = secretValid ? null : "Provider signature could not be validated.";
        ReceivedAtUtc = DateTime.UtcNow;
        ProcessingAttempts = 0;
    }

    public void MarkProcessing()
    {
        if (Status == PaymentProviderEventStatus.Processed)
        {
            return;
        }

        Status = PaymentProviderEventStatus.Processing;
        ProcessingStartedAtUtc = DateTime.UtcNow;
        ProcessingAttempts++;
    }

    public void MarkProcessed()
    {
        Status = PaymentProviderEventStatus.Processed;
        ProcessedAtUtc = DateTime.UtcNow;
        FailureReason = null;
    }

    public void MarkFailed(string? failureReason)
    {
        Status = PaymentProviderEventStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? "Unknown failure" : failureReason.Trim();
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkIgnored(string? reason)
    {
        Status = PaymentProviderEventStatus.Ignored;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ProcessedAtUtc = DateTime.UtcNow;
    }
}
