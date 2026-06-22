using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Security.Cryptography;
using System.Text;
using Zadana.Infrastructure.Persistence.Encryption;

namespace Zadana.Application.Tests.Application.Security;

public class EncryptedStringConverterTests
{
    [Fact]
    public void V2Ciphertext_ShouldDecryptAcrossIndependentConverterInstances()
    {
        var masterKey = SHA256.HashData(Encoding.UTF8.GetBytes("stable-production-key"));
        var first = PiiProtector.CreateConverter(masterKey);
        var second = PiiProtector.CreateConverter(masterKey);

        var encrypted = ConvertToProvider(first, "NID260622053113");
        var decrypted = ConvertFromProvider(second, encrypted);

        encrypted.Should().StartWith("enc:v2:");
        encrypted.Should().NotContain("NID260622053113");
        decrypted.Should().Be("NID260622053113");
    }

    [Fact]
    public void V2Ciphertext_WithDifferentKey_ShouldFailClosed()
    {
        var first = PiiProtector.CreateConverter(
            SHA256.HashData(Encoding.UTF8.GetBytes("first-key")));
        var second = PiiProtector.CreateConverter(
            SHA256.HashData(Encoding.UTF8.GetBytes("second-key")));

        var encrypted = ConvertToProvider(first, "sensitive-value");

        ConvertFromProvider(second, encrypted).Should().BeNull();
    }

    private static string? ConvertToProvider(ValueConverter converter, string? value) =>
        (string?)converter.ConvertToProvider(value);

    private static string? ConvertFromProvider(ValueConverter converter, string? value) =>
        (string?)converter.ConvertFromProvider(value);
}
