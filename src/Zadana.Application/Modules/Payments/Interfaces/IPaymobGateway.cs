using Zadana.Application.Modules.Payments.DTOs;

namespace Zadana.Application.Modules.Payments.Interfaces;

public interface IPaymobGateway
{
    bool IsEnabled { get; }

    Task<PaymobCheckoutSessionDto> CreateCheckoutSessionAsync(
        PaymobCheckoutSessionRequest request,
        CancellationToken cancellationToken = default);

    PaymobWebhookNotificationDto ParseWebhookNotification(string rawPayload);
}
