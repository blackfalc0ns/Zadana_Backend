using FluentAssertions;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Checkout;

public class CheckoutSupportTests
{
    [Fact]
    public void NormalizePaymentMethodCode_WhenMadaRequested_ShouldUseCardGateway()
    {
        CheckoutSupport.NormalizePaymentMethodCode("mada").Should().Be("card");
        CheckoutSupport.MapPaymentMethodCodeToEnumName("mada").Should().Be("Card");
    }

    [Fact]
    public void NormalizePaymentMethodCode_WhenWalletRequested_ShouldRejectExplicitly()
    {
        var act = () => CheckoutSupport.NormalizePaymentMethodCode("wallet");

        act.Should()
            .Throw<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "PAYMENT_METHOD_NOT_SUPPORTED");
    }
}
