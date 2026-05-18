using FluentAssertions;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;

namespace Zadana.UnitTests.Modules.Finance;

public class CurrencyPolicyTests
{
    [Theory]
    [InlineData("SAR")]
    [InlineData("sar")]
    [InlineData(" Sar ")]
    [InlineData(null)]
    [InlineData("")]
    public void Normalize_returns_official_currency_for_sar_and_blank(string? input)
    {
        CurrencyPolicy.Normalize(input).Should().Be("SAR");
    }

    [Theory]
    [InlineData("EGP")]
    [InlineData("USD")]
    [InlineData("AED")]
    public void EnsureOfficial_throws_for_non_sar(string currency)
    {
        var act = () => CurrencyPolicy.EnsureOfficial(currency);
        act.Should().Throw<BusinessRuleException>()
            .Which.ErrorCode.Should().Be("UNSUPPORTED_CURRENCY");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 1000)]
    [InlineData(130.75, 13075)]
    [InlineData(99.999, 10000)] // away-from-zero rounding
    public void ToMinorUnits_uses_halalas_for_sar(decimal amount, long expected)
    {
        CurrencyPolicy.ToMinorUnits(amount, "SAR").Should().Be(expected);
    }

    [Fact]
    public void ToMinorUnits_rejects_non_sar_currency()
    {
        var act = () => CurrencyPolicy.ToMinorUnits(100m, "EGP");
        act.Should().Throw<BusinessRuleException>();
    }

    [Theory]
    [InlineData(1000, 10.00)]
    [InlineData(13075, 130.75)]
    public void FromMinorUnits_round_trips(long minor, decimal expected)
    {
        CurrencyPolicy.FromMinorUnits(minor).Should().Be(expected);
    }
}
