namespace Zadana.Domain.Modules.Payments.Enums;

public static class PaymentMethodTypeExtensions
{
    /// <summary>
    /// Online gateway methods that confirm via Moyasar and should move to vendor acceptance when paid.
    /// </summary>
    public static bool IsOnlineGatewayMethod(this PaymentMethodType method) =>
        method is PaymentMethodType.Card or PaymentMethodType.ApplePay or PaymentMethodType.Mada;
}
