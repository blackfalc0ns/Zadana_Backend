using FluentAssertions;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Wallets.Commands.CompleteVendorPayout;

namespace Zadana.UnitTests.Modules.Finance;

public sealed class CompleteVendorPayoutCommandValidatorTests
{
    [Fact]
    public void Validate_rejects_missing_or_empty_protected_proof_attachment()
    {
        var validator = new CompleteVendorPayoutCommandValidator(CreateLocalizer());

        var missing = validator.Validate(new CompleteVendorPayoutCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BANK-REF-123",
            null));
        var empty = validator.Validate(new CompleteVendorPayoutCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BANK-REF-124",
            Guid.Empty));

        missing.IsValid.Should().BeFalse();
        empty.IsValid.Should().BeFalse();
        missing.Errors.Should().Contain(error => error.ErrorCode == "PAYOUT_PROOF_REQUIRED");
        empty.Errors.Should().Contain(error => error.ErrorCode == "PAYOUT_PROOF_REQUIRED");
    }

    [Fact]
    public void Validate_accepts_a_protected_proof_attachment_id()
    {
        var validator = new CompleteVendorPayoutCommandValidator(CreateLocalizer());

        var result = validator.Validate(new CompleteVendorPayoutCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BANK-REF-HTTPS",
            Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    private static IStringLocalizer<SharedResource> CreateLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<SharedResource>>();
        localizer.Setup(item => item[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        return localizer.Object;
    }
}
