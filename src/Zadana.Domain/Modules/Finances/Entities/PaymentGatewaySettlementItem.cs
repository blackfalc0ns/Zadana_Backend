using Zadana.Domain.Modules.Finances.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Finances.Entities;

/// <summary>
/// One line inside a <see cref="PaymentGatewaySettlement"/> linking a provider
/// payment id back to an internal Order/Payment, with per-payment fee and net.
/// </summary>
public class PaymentGatewaySettlementItem : BaseEntity
{
    public Guid SettlementId { get; private set; }
    public string ProviderPaymentId { get; private set; } = null!;
    public Guid? OrderId { get; private set; }
    public Guid? PaymentId { get; private set; }

    public decimal GrossAmount { get; private set; }
    public decimal FeeAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public string CurrencyCode { get; private set; } = CurrencyPolicy.OfficialCurrency;

    public DateTime? ProviderCreatedAtUtc { get; private set; }
    public string? Metadata { get; private set; }
    public PaymentGatewaySettlementMatchStatus MatchStatus { get; private set; }
    public string? MatchNote { get; private set; }

    public PaymentGatewaySettlement Settlement { get; private set; } = null!;

    private PaymentGatewaySettlementItem() { }

    public PaymentGatewaySettlementItem(
        Guid settlementId,
        string providerPaymentId,
        decimal grossAmount,
        decimal feeAmount,
        decimal netAmount,
        string? currencyCode = null,
        Guid? orderId = null,
        Guid? paymentId = null,
        DateTime? providerCreatedAtUtc = null,
        string? metadata = null,
        PaymentGatewaySettlementMatchStatus matchStatus = PaymentGatewaySettlementMatchStatus.Matched,
        string? matchNote = null)
    {
        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            throw new BusinessRuleException("INVALID_PROVIDER_PAYMENT_ID", "Provider payment id is required.");
        }

        if (grossAmount < 0 || feeAmount < 0)
        {
            throw new BusinessRuleException("INVALID_SETTLEMENT_ITEM_AMOUNTS", "Settlement item amounts cannot be negative.");
        }

        var normalized = CurrencyPolicy.Normalize(currencyCode);
        CurrencyPolicy.EnsureOfficial(normalized);

        SettlementId = settlementId;
        ProviderPaymentId = providerPaymentId.Trim();
        OrderId = orderId;
        PaymentId = paymentId;
        GrossAmount = grossAmount;
        FeeAmount = feeAmount;
        NetAmount = netAmount;
        CurrencyCode = normalized;
        ProviderCreatedAtUtc = providerCreatedAtUtc;
        Metadata = metadata;
        MatchStatus = matchStatus;
        MatchNote = string.IsNullOrWhiteSpace(matchNote) ? null : matchNote.Trim();
    }

    public void UpdateMatch(PaymentGatewaySettlementMatchStatus status, Guid? orderId = null, Guid? paymentId = null, string? note = null)
    {
        MatchStatus = status;
        if (orderId.HasValue) OrderId = orderId;
        if (paymentId.HasValue) PaymentId = paymentId;
        if (!string.IsNullOrWhiteSpace(note)) MatchNote = note.Trim();
    }
}
