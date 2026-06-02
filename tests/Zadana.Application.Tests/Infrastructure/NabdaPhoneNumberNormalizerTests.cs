using FluentAssertions;
using Zadana.Infrastructure.Services;

namespace Zadana.Application.Tests.Infrastructure;

public class NabdaPhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("01012345678", "+20", "+201012345678")]
    [InlineData("+201012345678", "+20", "+201012345678")]
    [InlineData("+966501234567", "+20", "+966501234567")]
    [InlineData("010 1234-5678", "+20", "+201012345678")]
    [InlineData("(010) 1234-5678", "+20", "+201012345678")]
    [InlineData("501234567", "+966", "+966501234567")]
    public void Normalize_WithSupportedFormats_ReturnsInternationalPhone(
        string input,
        string countryCode,
        string expected)
    {
        var result = NabdaPhoneNumberNormalizer.Normalize(input, countryCode);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("++201012345678")]
    [InlineData("+123")]
    public void Normalize_WithInvalidPhone_Throws(string input)
    {
        var act = () => NabdaPhoneNumberNormalizer.Normalize(input, "+20");

        act.Should().Throw<ArgumentException>();
    }
}
