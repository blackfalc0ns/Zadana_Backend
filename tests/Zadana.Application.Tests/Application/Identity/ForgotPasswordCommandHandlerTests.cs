using FluentAssertions;
using Moq;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.ForgotPassword;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Interfaces;

namespace Zadana.Application.Tests.Application.Identity;

/// <summary>
/// Unit tests for ForgotPasswordCommandHandler.
/// </summary>
public class ForgotPasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IOtpService> _otpServiceMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock = new();

    private ForgotPasswordCommandHandler CreateHandler() =>
        new(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _otpServiceMock.Object,
            _emailServiceMock.Object,
            _localizerMock.Object
        );

    private void SetupLocalizer()
    {
        _localizerMock.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        _localizerMock.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));
    }

    // ─── User Not Found (silent return) ────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnSilently()
    {
        // Arrange — prevents email enumeration
        SetupLocalizer();
        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ForgotPasswordCommand("unknown@test.com");
        var handler = CreateHandler();

        // Act — should not throw
        await handler.Handle(command, CancellationToken.None);

        // Assert — OTP and email should NOT be sent
        _otpServiceMock.Verify(
            o => o.SendOtpSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _emailServiceMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── User Found → Send OTP via SMS ─────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserFound_ShouldSendOtpViaSmsAndSave()
    {
        // Arrange
        SetupLocalizer();
        var user = new User("Test", "test@zadana.com", "01011111111", "hash",
            Zadana.Domain.Modules.Identity.Enums.UserRole.Customer);

        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync("01011111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ForgotPasswordCommand("01011111111");
        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _otpServiceMock.Verify(
            o => o.SendOtpSmsAsync(user.Phone, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _userRepositoryMock.Verify(r => r.Update(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── User with Email → Send both SMS and Email ─────────────────────────

    [Fact]
    public async Task Handle_WhenUserHasEmail_ShouldSendEmailToo()
    {
        // Arrange
        SetupLocalizer();
        var user = new User("Test", "user@zadana.com", "01022222222", "hash",
            Zadana.Domain.Modules.Identity.Enums.UserRole.Customer);

        _userRepositoryMock
            .Setup(r => r.GetByIdentifierAsync("user@zadana.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ForgotPasswordCommand("user@zadana.com");
        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(
            e => e.SendEmailAsync(user.Email!, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
