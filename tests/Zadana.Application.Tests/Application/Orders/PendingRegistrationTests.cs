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
}
