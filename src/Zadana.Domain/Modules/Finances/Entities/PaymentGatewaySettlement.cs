using Zadana.Domain.Modules.Finances.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Finances.Entities;

/// <summary>
/// Represents a settlement report received from a payment gateway (e.g. Moyasar)
/// describing money the gateway transferred to the platform's bank account
/// on a given settlement date. See section 16 of the revised spec.
/// </summary>
public class PaymentGatewaySettlement : BaseEntity
{
    private readonly List<PaymentGatewaySettlementItem> _items = [];

    public string ProviderName { get; private set; } = null!;
    public string ProviderSettlementId { get; private set; } = null!;

    public DateTime SettlementDate { get; private set; }
    public string CurrencyCode { get; private set; } = CurrencyPolicy.OfficialCurrency;

    public decimal GrossAmount { get; private set; }
    public decimal FeeAmount { get; private set; }
    public decimal NetAmount { get; private set; }

    public PaymentGatewaySettlementStatus Status { get; private set; }
    public string? RawFileOrJson { get; private set; }
    public string? Notes { get; private set; }

    public Guid? FinancialEventId { get; private set; }

    public IReadOnlyCollection<PaymentGatewaySettlementItem> Items => _items.AsReadOnly();

    private PaymentGatewaySettlement() { }

    public PaymentGatewaySettlement(
        string providerName,
        string providerSettlementId,
        DateTime settlementDate,
        decimal grossAmount,
        decimal feeAmount,
        decimal netAmount,
        string? currencyCode = null,
        string? rawFileOrJson = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new BusinessRuleException("INVALID_PROVIDER_NAME", "Provider name is required.");
        }

        if (string.IsNullOrWhiteSpace(providerSettlementId))
        {
            throw new BusinessRuleException("INVALID_PROVIDER_SETTLEMENT_ID", "Provider settlement id is required.");
        }

        if (grossAmount < 0 || feeAmount < 0)
        {
            throw new BusinessRuleException("INVALID_SETTLEMENT_AMOUNTS", "Gateway settlement amounts cannot be negative.");
        }

        var normalized = CurrencyPolicy.Normalize(currencyCode);
        CurrencyPolicy.EnsureOfficial(normalized);

        ProviderName = providerName.Trim();
        ProviderSettlementId = providerSettlementId.Trim();
        SettlementDate = settlementDate;
        CurrencyCode = normalized;
        GrossAmount = grossAmount;
        FeeAmount = feeAmount;
        NetAmount = netAmount;
        Status = PaymentGatewaySettlementStatus.Imported;
        RawFileOrJson = rawFileOrJson;
        Notes = notes?.Trim();
    }

    public void AddItem(PaymentGatewaySettlementItem item)
    {
        if (item.CurrencyCode != CurrencyCode)
        {
            throw new BusinessRuleException(
                "SETTLEMENT_ITEM_CURRENCY_MISMATCH",
                "Settlement item currency must match the parent settlement currency.");
        }

        _items.Add(item);
    }

    public void MarkReconciled() => Status = PaymentGatewaySettlementStatus.Reconciled;
    public void MarkVariance(string? note = null)
    {
        Status = PaymentGatewaySettlementStatus.Variance;
        if (!string.IsNullOrWhiteSpace(note)) Notes = note.Trim();
    }

    public void MarkPosted(Guid financialEventId)
    {
        Status = PaymentGatewaySettlementStatus.Posted;
        FinancialEventId = financialEventId;
    }
}
