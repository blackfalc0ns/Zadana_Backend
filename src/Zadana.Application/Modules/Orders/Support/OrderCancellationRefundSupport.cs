using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Application.Modules.Orders.Support;

public static class OrderCancellationRefundSupport
{
    public static async Task TryRefundPaidOrderAsync(
        IApplicationDbContext context,
        IPaymentGatewayResolver? gatewayResolver,
        ILogger? logger,
        Order order,
        string reason,
        CancellationToken cancellationToken = default)
    {
        // Cash-on-pickup was never captured online — nothing to refund.
        if (order.Fulfillment == FulfillmentType.Pickup &&
            order.PaymentMethod == PaymentMethodType.CashOnDelivery)
        {
            return;
        }

        if (order.PaymentStatus != PaymentStatus.Paid)
        {
            return;
        }

        var payment = await context.Payments
            .Where(item => item.OrderId == order.Id && item.Status == PaymentStatus.Paid)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null)
        {
            return;
        }

        var previousRefundsTotal = await context.Refunds
            .Where(item => item.PaymentId == payment.Id && item.LifecycleStatus == RefundStatus.Succeeded)
            .SumAsync(item => item.Amount, cancellationToken);

        var refundableAmount = order.TotalAmount - previousRefundsTotal;
        if (refundableAmount <= 0m)
        {
            return;
        }

        var refund = new Refund(payment.Id, refundableAmount, reason, "same_method", "Platform");
        context.Refunds.Add(refund);

        if (payment.Method == PaymentMethodType.Card &&
            !string.IsNullOrWhiteSpace(payment.ProviderTransactionId) &&
            !string.IsNullOrWhiteSpace(payment.ProviderName) &&
            gatewayResolver is not null &&
            gatewayResolver.TryResolve(payment.ProviderName, out var gateway) &&
            gateway!.IsEnabled)
        {
            try
            {
                var refundResult = await gateway.RefundAsync(
                    new RefundGatewayCommand(
                        refund.Id,
                        payment.ProviderTransactionId,
                        refundableAmount,
                        payment.Currency,
                        Guid.NewGuid().ToString("N"),
                        reason),
                    cancellationToken);

                if (refundResult.ProviderRefundId is not null &&
                    refundResult.ProviderStatus is not "failed")
                {
                    refund.Process();
                }
                else
                {
                    refund.Fail(refundResult.FailureMessage);
                    logger?.LogWarning(
                        "Gateway refund failed for order {OrderId} refund {RefundId}: {Reason}",
                        order.Id,
                        refund.Id,
                        refundResult.FailureMessage);
                    return;
                }
            }
            catch (Exception ex)
            {
                refund.Fail(ex.Message);
                logger?.LogError(ex, "Gateway refund exception for order {OrderId} refund {RefundId}", order.Id, refund.Id);
                return;
            }
        }
        else
        {
            refund.Process();
        }

        order.UpdatePaymentStatus(
            refundableAmount >= order.TotalAmount
                ? PaymentStatus.Refunded
                : PaymentStatus.PartiallyRefunded);
    }
}
