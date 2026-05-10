using Zadana.Application.Modules.Payments.DTOs;

namespace Zadana.Application.Modules.Payments.Interfaces;

public interface IPaymobPayoutGateway
{
    bool IsEnabled { get; }

    Task<PaymobPayoutResult> TriggerPayoutAsync(PaymobPayoutRequest request, CancellationToken cancellationToken = default);

    PaymobPayoutWebhookNotification ParsePayoutWebhook(string rawPayload);
}
