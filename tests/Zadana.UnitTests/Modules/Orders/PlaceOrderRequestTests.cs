using System.Text.Json;
using FluentAssertions;
using Zadana.Api.Modules.Orders.Requests;

namespace Zadana.UnitTests.Modules.Orders;

public class PlaceOrderRequestTests
{
    [Fact]
    public void Deserialize_UsesSnakeCaseContractValues()
    {
        const string json = """
            {
              "vendor_id": "11111111-1111-1111-1111-111111111111",
              "address_id": "22222222-2222-2222-2222-222222222222",
              "delivery_slot_id": "standard-30-45",
              "payment_method": "cash",
              "promo_code": "RAMADAN",
              "notes": "leave at door"
            }
            """;

        var request = JsonSerializer.Deserialize<PlaceOrderRequest>(json);

        request.Should().NotBeNull();
        request!.EffectiveVendorId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        request.EffectiveAddressId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        request.EffectiveDeliverySlotId.Should().Be("standard-30-45");
        request.EffectivePaymentMethod.Should().Be("cash");
        request.EffectivePromoCode.Should().Be("RAMADAN");
        request.EffectiveNotes.Should().Be("leave at door");
    }

    [Fact]
    public void Deserialize_AcceptsCamelCaseAndLegacyPaymentAliases()
    {
        const string json = """
            {
              "vendorId": "33333333-3333-3333-3333-333333333333",
              "addressId": "44444444-4444-4444-4444-444444444444",
              "deliverySlotId": "standard-30-45",
              "paymentMethod": "cash_on_delivery",
              "promoCode": "SAVE10",
              "note": "call on arrival"
            }
            """;

        var request = JsonSerializer.Deserialize<PlaceOrderRequest>(json);

        request.Should().NotBeNull();
        request!.EffectiveVendorId.Should().Be(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        request.EffectiveAddressId.Should().Be(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        request.EffectiveDeliverySlotId.Should().Be("standard-30-45");
        request.EffectivePaymentMethod.Should().Be("cash");
        request.EffectivePromoCode.Should().Be("SAVE10");
        request.EffectiveNotes.Should().Be("call on arrival");
    }

    [Theory]
    [InlineData("bank_transfer", "bank")]
    [InlineData("credit_card", "card")]
    [InlineData("debit_card", "card")]
    [InlineData("applePay", "apple_pay")]
    public void Deserialize_NormalizesLegacyPaymentMethodAliases(string rawPaymentMethod, string expected)
    {
        var json = $$"""
            {
              "addressId": "55555555-5555-5555-5555-555555555555",
              "paymentMethod": "{{rawPaymentMethod}}"
            }
            """;

        var request = JsonSerializer.Deserialize<PlaceOrderRequest>(json);

        request.Should().NotBeNull();
        request!.EffectivePaymentMethod.Should().Be(expected);
    }
}
