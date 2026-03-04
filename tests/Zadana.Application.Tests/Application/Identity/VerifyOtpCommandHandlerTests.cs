using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Commands.VerifyOtp;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Identity;

/// <summary>
/// Unit tests for VerifyOtpCommandHandler.
/// </summary>
public class VerifyOtpCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private VerifyOtpCommandHandler CreateHandler() =>
        new(_userRepositoryMock.Object, _unitOfWorkMock.Object);

    private User CreateUserWithValidOtp(out string otp)
    {
        var user = new User("Test User", "test@zadana.com", "01011111111", "hash",
            Zadana.Domain.Modules.Identity.Enums.UserRole.Customer);
        otp = user.GenerateOtp();
        return user;
    }

    // ─── User Not Found ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new VerifyOtpCommand("unknown_user@test.com", "1234");
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ─── Wrong OTP ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithWrongOtpCode_ShouldThrowBusinessRuleException()
    {
        // Arrange
        var user = CreateUserWithValidOtp(out _);

        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync("01011111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new VerifyOtpCommand("01011111111", "0000"); // Wrong code
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "INVALID_OTP",
                "the exception should carry the INVALID_OTP error code");
    }

    // ─── Valid OTP ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithCorrectOtpCode_ShouldReturnTrue_AndSaveChanges()
    {
        // Arrange
        var user = CreateUserWithValidOtp(out var otp);

        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync("01011111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new VerifyOtpCommand("01011111111", otp);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once,
            "SaveChanges must be called after successful verification");
        _userRepositoryMock.Verify(r => r.Update(user), Times.Once);
    }
}
