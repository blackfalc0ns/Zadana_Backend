using FluentAssertions;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Tests.Identity;

public class UserEntityTests
{
    private static User CreateTestUser() =>
        new(
            fullName: "Test User",
            email: "test@zadana.com",
            phone: "01011111111",
            role: UserRole.Customer);

    [Fact]
    public void GenerateOtp_ShouldReturn4DigitString()
    {
        var user = CreateTestUser();
        var otp = user.GenerateOtp();

        otp.Should().NotBeNullOrEmpty();
        otp.Should().HaveLength(4);
        int.TryParse(otp, out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateOtp_ShouldStoreHashedOtpCodeOnUser()
    {
        var user = CreateTestUser();
        var otp = user.GenerateOtp();

        user.OtpCode.Should().NotBe(otp);
        user.VerifyOtp(otp).Should().BeTrue();
    }

    [Fact]
    public void GenerateOtp_ShouldInvalidatePreviousRegistrationCode()
    {
        var user = CreateTestUser();
        var firstOtp = user.GenerateOtp();
        var secondOtp = user.GenerateOtp();

        user.VerifyOtp(firstOtp).Should().BeFalse();
        user.VerifyOtp(secondOtp).Should().BeTrue();
    }

    [Fact]
    public void GeneratePasswordResetOtp_ShouldInvalidatePreviousPasswordResetCode()
    {
        var user = CreateTestUser();
        var firstOtp = user.GeneratePasswordResetOtp();
        var secondOtp = user.GeneratePasswordResetOtp();

        user.ConfirmPasswordResetOtp(firstOtp).Should().BeNull();
        user.ConfirmPasswordResetOtp(secondOtp).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateOtp_ShouldClearActivePasswordResetOtp()
    {
        var user = CreateTestUser();
        var resetOtp = user.GeneratePasswordResetOtp();
        var registrationOtp = user.GenerateOtp();

        user.ConfirmPasswordResetOtp(resetOtp).Should().BeNull();
        user.VerifyOtp(registrationOtp).Should().BeTrue();
    }

    [Fact]
    public void GeneratePasswordResetOtp_ShouldClearPendingRegistrationOtp()
    {
        var user = CreateTestUser();
        var registrationOtp = user.GenerateOtp();
        var resetOtp = user.GeneratePasswordResetOtp();

        user.VerifyOtp(registrationOtp).Should().BeFalse();
        user.ConfirmPasswordResetOtp(resetOtp).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void VerifyPasswordResetOtp_ExpiredOtp_ReturnsFalseAndClearsResetOtp()
    {
        var user = CreateTestUser();
        var code = user.GeneratePasswordResetOtp();

        typeof(User).GetProperty(nameof(User.PasswordResetOtpExpiry))!
            .SetValue(user, DateTime.UtcNow.AddMinutes(-1));

        user.VerifyPasswordResetOtp(code).Should().BeFalse();
        user.PasswordResetOtp.Should().BeNull();
        user.PasswordResetOtpExpiry.Should().BeNull();
    }
}
