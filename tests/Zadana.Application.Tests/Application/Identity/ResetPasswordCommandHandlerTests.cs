using FluentAssertions;
using Moq;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.ResetPassword;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Identity;

/// <summary>
/// Unit tests for ResetPasswordCommandHandler.
/// </summary>
public class ResetPasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock = new();

    private ResetPasswordCommandHandler CreateHandler() =>
        new(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _localizerMock.Object
        );

    private void SetupLocalizer()
    {
        _localizerMock.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
    }

    // ─── User Not Found ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldThrowUnauthorizedException()
    {
        // Arrange
        SetupLocalizer();
        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ResetPasswordCommand("unknown@test.com", "1234", "NewP@ssword1");
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    // ─── Invalid OTP ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithInvalidOtp_ShouldThrowBusinessRuleException()
    {
        // Arrange
        SetupLocalizer();
        var user = new User("Test", "test@zadana.com", "01011111111", "hash",
            Zadana.Domain.Modules.Identity.Enums.UserRole.Customer);
        // Generate a real OTP so VerifyPasswordResetOtp exists, but pass wrong code
        user.GeneratePasswordResetOtp();

        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync("01011111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetPasswordCommand("01011111111", "0000", "NewP@ssword1");
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "INVALID_OTP");
    }

    // ─── Valid Reset ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidOtp_ShouldChangePasswordAndSave()
    {
        // Arrange
        SetupLocalizer();
        var user = new User("Test", "test@zadana.com", "01011111111", "hash",
            Zadana.Domain.Modules.Identity.Enums.UserRole.Customer);
        var otp = user.GeneratePasswordResetOtp();

        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync("01011111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(p => p.HashPassword("NewP@ssword1"))
            .Returns("new_hashed_password");

        var command = new ResetPasswordCommand("01011111111", otp, "NewP@ssword1");
        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _passwordHasherMock.Verify(p => p.HashPassword("NewP@ssword1"), Times.Once);
        _userRepositoryMock.Verify(r => r.Update(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
