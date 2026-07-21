using FluentAssertions;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Wallets.Commands.CompleteVendorPayout;

namespace Zadana.UnitTests.Modules.Finance;

public sealed class CompleteVendorPayoutCommandValidatorTests
{
    [Theory]
    [InlineData("ftp://files.zadna0.com/payout-proof.pdf")]
    [InlineData("/payout-proof.pdf")]
    [InlineData("not-a-url")]
    public void Validate_rejects_non_http_proof_urls(string proofUrl)
    {
        var validator = new CompleteVendorPayoutCommandValidator(CreateLocalizer());

        var result = validator.Validate(new CompleteVendorPayoutCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BANK-REF-123",
            proofUrl));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == "PAYOUT_PROOF_URL_INVALID");
    }

    [Fact]
    public void Validate_accepts_http_or_https_proof_urls()
    {
        var validator = new CompleteVendorPayoutCommandValidator(CreateLocalizer());

        var httpResult = validator.Validate(new CompleteVendorPayoutCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BANK-REF-HTTP",
            "http://files.zadna0.com/payout-proof.pdf"));
        var httpsResult = validator.Validate(new CompleteVendorPayoutCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BANK-REF-HTTPS",
            "https://files.zadna0.com/payout-proof.pdf"));

        httpResult.IsValid.Should().BeTrue();
        httpsResult.IsValid.Should().BeTrue();
    }

    private static IStringLocalizer<SharedResource> CreateLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<SharedResource>>();
        localizer.Setup(item => item[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        return localizer.Object;
    }
}
