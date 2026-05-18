namespace Zadana.Domain.Modules.Payments.Enums;

/// <summary>
/// High-level classification of the payment channel exposed to clients.
/// Maps an internal <see cref="PaymentMethodType"/> + provider into one of the
/// six labels declared in section 5.2 of the revised spec.
/// </summary>
public enum PaymentMethodChannel
{
    Card = 0,
    ApplePay = 1,
    SamsungPay = 2,
    StcPay = 3,
    Cash = 4,
    Bank = 5,
}
