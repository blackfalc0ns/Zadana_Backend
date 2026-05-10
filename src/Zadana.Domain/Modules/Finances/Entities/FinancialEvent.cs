using Zadana.Domain.Modules.Finances.Enums;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Finances.Entities;

public class FinancialEvent : BaseEntity
{
    public FinancialEventType EventType { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public Guid? OrderId { get; private set; }
    public Guid? SettlementId { get; private set; }
    public Guid? PayoutId { get; private set; }
    public Guid? RefundId { get; private set; }
    public string CurrencyCode { get; private set; } = "EGP";
    public DateTime OccurredAtUtc { get; private set; }
    public string? Description { get; private set; }

    public JournalEntry? JournalEntry { get; private set; }

    private FinancialEvent() { }

    public FinancialEvent(
        FinancialEventType eventType,
        string idempotencyKey,
        Guid? orderId = null,
        Guid? settlementId = null,
        Guid? payoutId = null,
        Guid? refundId = null,
        string currencyCode = "EGP",
        Guid? correlationId = null,
        DateTime? occurredAtUtc = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        EventType = eventType;
        IdempotencyKey = idempotencyKey.Trim();
        OrderId = orderId;
        SettlementId = settlementId;
        PayoutId = payoutId;
        RefundId = refundId;
        CurrencyCode = NormalizeCurrency(currencyCode);
        CorrelationId = correlationId ?? Guid.NewGuid();
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static string NormalizeCurrency(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return "EGP";
        }

        return currencyCode.Trim().ToUpperInvariant();
    }
}
