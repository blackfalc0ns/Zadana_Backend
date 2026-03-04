using FluentAssertions;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Application.Tests.Domain.Identity;

/// <summary>
/// Tests for User entity Domain business logic (OTP generation and verification).
/// These are pure unit tests with NO dependencies.
/// </summary>
public class UserEntityTests
{
    private User CreateTestUser()
    {
        return new User(
            fullName: "Test User",
            email: "test@zadana.com",
            phone: "01011111111",
            passwordHash: "hashed_password_123",
            role: Zadana.Domain.Modules.Identity.Enums.UserRole.Customer
        );
    }

    // ─── GenerateOtp Tests ─────────────────────────────────────────────────

    [Fact]
    public void GenerateOtp_ShouldReturn4DigitString()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var otp = user.GenerateOtp();

        // Assert
        otp.Should().NotBeNullOrEmpty();
        otp.Should().HaveLength(4);
        int.TryParse(otp, out _).Should().BeTrue("OTP must be a numeric string");
    }

    [Fact]
    public void GenerateOtp_ShouldSetOtpCodeOnUser()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var otp = user.GenerateOtp();

        // Assert
        user.OtpCode.Should().Be(otp, "OtpCode property must match the generated code");
    }

    [Fact]
    public void GenerateOtp_ShouldSetExpiryTimeApprox5MinutesFromNow()
    {
        // Arrange
        var user = CreateTestUser();
        var before = DateTime.UtcNow.AddMinutes(4.9);

        // Act
        user.GenerateOtp();
        var after = DateTime.UtcNow.AddMinutes(5.1);

        // Assert
        user.OtpExpiryTime.Should().NotBeNull();
        user.OtpExpiryTime!.Value.Should().BeAfter(before);
        user.OtpExpiryTime!.Value.Should().BeBefore(after);
    }

    // ─── VerifyOtp Tests ───────────────────────────────────────────────────

    [Fact]
    public void VerifyOtp_WithCorrectCode_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var otp = user.GenerateOtp();

        // Act
        var result = user.VerifyOtp(otp);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyOtp_WithCorrectCode_ShouldMarkPhoneAsVerified()
    {
        // Arrange
        var user = CreateTestUser();
        var otp = user.GenerateOtp();

        // Act
        user.VerifyOtp(otp);

        // Assert
        user.IsPhoneVerified.Should().BeTrue();
    }

    [Fact]
    public void VerifyOtp_AfterSuccess_ShouldClearOtpCode()
    {
        // Arrange
        var user = CreateTestUser();
        var otp = user.GenerateOtp();

        // Act
        user.VerifyOtp(otp);

        // Assert
        user.OtpCode.Should().BeNull("OTP code should be cleared after successful verification");
        user.OtpExpiryTime.Should().BeNull();
    }

    [Fact]
    public void VerifyOtp_WithWrongCode_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateTestUser();
        user.GenerateOtp();

        // Act
        var result = user.VerifyOtp("0000"); // Wrong code

        // Assert
        result.Should().BeFalse();
        user.IsPhoneVerified.Should().BeFalse("phone should NOT be verified with wrong code");
    }

    [Fact]
    public void VerifyOtp_WithNoGeneratedOtp_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateTestUser(); // No OTP generated

        // Act
        var result = user.VerifyOtp("1234");

        // Assert
        result.Should().BeFalse("no OTP exists to match against");
    }

    // ─── Password Reset OTP Tests ──────────────────────────────────────────

    [Fact]
    public void GeneratePasswordResetOtp_SetsOtpAnd15MinuteExpiry()
    {
        // Arrange
        var user = CreateTestUser();
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var code = user.GeneratePasswordResetOtp();

        // Assert
        code.Should().NotBeNullOrWhiteSpace();
        code.Length.Should().Be(4); // Since it's between 1000 and 9999
        code.Should().Be(user.PasswordResetOtp);
        
        user.PasswordResetOtpExpiry.Should().NotBeNull();
        user.PasswordResetOtpExpiry.Value.Should().BeOnOrAfter(beforeGeneration.AddMinutes(15));
        user.PasswordResetOtpExpiry.Value.Should().BeBefore(DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public void VerifyPasswordResetOtp_ValidCode_ReturnsTrueAndClearsResetOtp()
    {
        // Arrange
        var user = CreateTestUser();
        var code = user.GeneratePasswordResetOtp();

        // Act
        var result = user.VerifyPasswordResetOtp(code);

        // Assert
        result.Should().BeTrue();
        user.PasswordResetOtp.Should().BeNull();
        user.PasswordResetOtpExpiry.Should().BeNull();
    }

    [Fact]
    public void VerifyPasswordResetOtp_InvalidCode_ReturnsFalseAndLeavesResetOtpIntact()
    {
        // Arrange
        var user = CreateTestUser();
        user.GeneratePasswordResetOtp();
        var invalidCode = "0000";

        // Act
        var result = user.VerifyPasswordResetOtp(invalidCode);

        // Assert
        result.Should().BeFalse();
        user.PasswordResetOtp.Should().NotBeNull();
        user.PasswordResetOtpExpiry.Should().NotBeNull();
    }

    [Fact]
    public void VerifyPasswordResetOtp_ExpiredOtp_ReturnsFalseAndLeavesResetOtpIntact()
    {
        // Arrange
        var user = CreateTestUser();
        user.GeneratePasswordResetOtp();
        
        // Use reflection to forcefully expire the OTP for testing
        var propertyInfo = typeof(User).GetProperty("PasswordResetOtpExpiry");
        propertyInfo?.SetValue(user, DateTime.UtcNow.AddMinutes(-1));

        var actualOtp = user.PasswordResetOtp;

        // Act
        var result = user.VerifyPasswordResetOtp(actualOtp!);

        // Assert
        result.Should().BeFalse();
        user.PasswordResetOtp.Should().NotBeNull();
        user.PasswordResetOtpExpiry.Should().NotBeNull();
    }
}
