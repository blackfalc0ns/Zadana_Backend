using FluentAssertions;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Tests.Application.Orders;

public class PendingRegistrationTests
{
    [Fact]
    public void GenerateAndVerifyOtp_ShouldSucceedForMatchingCode()
    {
        var pending = new PendingRegistration(
            "user@test.com",
            "0500000000",
            "hash",
            "User",
            UserRole.Customer,
            "{}");

        var code = pending.GenerateOtp();
        pending.VerifyOtp(code).Should().BeTrue();
    }

    [Fact]
    public void VerifyOtp_WithWrongCode_ShouldFail()
    {
        var pending = new PendingRegistration(
            "user@test.com",
            "0500000000",
            "hash",
            "User",
            UserRole.Customer,
            "{}");

        pending.GenerateOtp();
        pending.VerifyOtp("0000").Should().BeFalse();
        pending.IsExpired().Should().BeFalse();
    }

    [Fact]
    public void VerifyOtp_WhenExpiryIsUnspecifiedUtc_ShouldSucceed()
    {
        var pending = new PendingRegistration(
            "user@test.com",
            "0500000000",
            "hash",
            "User",
            UserRole.Customer,
            "{}");

        var code = pending.GenerateOtp();
        var rehydrated = PendingRegistration.Rehydrate(
            pending.Id,
            pending.Email,
            pending.PhoneNumber,
            pending.PasswordHash,
            pending.FullName,
            pending.Role,
            pending.PayloadJson,
            pending.ProfilePhotoUrl,
            pending.OtpCodeHash,
            DateTime.SpecifyKind(pending.OtpExpiryUtc!.Value, DateTimeKind.Unspecified),
            pending.OtpAttempts,
            DateTime.SpecifyKind(pending.LastOtpSentAtUtc!.Value, DateTimeKind.Unspecified),
            DateTime.SpecifyKind(pending.CreatedAtUtc, DateTimeKind.Unspecified),
            DateTime.SpecifyKind(pending.UpdatedAtUtc, DateTimeKind.Unspecified),
            DateTime.SpecifyKind(pending.ExpiresAtUtc, DateTimeKind.Unspecified));

        rehydrated.VerifyOtp(code).Should().BeTrue();
    }
}
