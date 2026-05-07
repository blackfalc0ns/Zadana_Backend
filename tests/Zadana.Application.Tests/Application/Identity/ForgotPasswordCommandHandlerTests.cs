using FluentAssertions;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Application.Common.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.Commands.ForgotPassword;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Tests.Application.Identity;

public class ForgotPasswordCommandHandlerTests
{
    private readonly Mock<IIdentityAccountService> _identityAccountServiceMock = new();
    private readonly Mock<IOtpService> _otpServiceMock = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock = new();

    private ForgotPasswordCommandHandler CreateHandler() =>
        new(
            _identityAccountServiceMock.Object,
            _otpServiceMock.Object,
            _localizerMock.Object);

    private static IdentityAccountSnapshot BuildAccount(string? email, string? phoneNumber) =>
        new(
            Guid.NewGuid(),
            "Test User",
            email,
            phoneNumber,
            UserRole.Customer,
            1,
            AccountStatus.Active,
            false,
            null,
            null,
            true,
            true);

    private void SetupLocalizer()
    {
        _localizerMock.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        _localizerMock.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args)));
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldReturnSilently()
    {
        SetupLocalizer();
        _identityAccountServiceMock
            .Setup(service => service.GeneratePasswordResetOtpAsync("unknown@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OtpDispatchResult(OtpDispatchStatus.UserNotFound));

        var handler = CreateHandler();

        await handler.Handle(new ForgotPasswordCommand("unknown@test.com"), CancellationToken.None);

        _otpServiceMock.Verify(
            service => service.SendOtpSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _otpServiceMock.Verify(
            service => service.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserHasPhone_ShouldSendSmsAndNotEmail()
    {
        SetupLocalizer();
        var account = BuildAccount(null, "01011111111");

        _identityAccountServiceMock
            .Setup(service => service.GeneratePasswordResetOtpAsync("01011111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OtpDispatchResult(OtpDispatchStatus.Succeeded, account, "123456"));

        var handler = CreateHandler();

        await handler.Handle(new ForgotPasswordCommand("01011111111"), CancellationToken.None);

        _otpServiceMock.Verify(
            service => service.SendOtpSmsAsync("01011111111", "123456", It.IsAny<CancellationToken>()),
            Times.Once);
        _otpServiceMock.Verify(
            service => service.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserHasEmailAndPhone_ShouldSendBothChannels()
    {
        SetupLocalizer();
        var account = BuildAccount("user@zadana.com", "01022222222");

        _identityAccountServiceMock
            .Setup(service => service.GeneratePasswordResetOtpAsync("user@zadana.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OtpDispatchResult(OtpDispatchStatus.Succeeded, account, "654321"));

        var handler = CreateHandler();

        await handler.Handle(new ForgotPasswordCommand("user@zadana.com"), CancellationToken.None);

        _otpServiceMock.Verify(
            service => service.SendOtpSmsAsync("01022222222", "654321", It.IsAny<CancellationToken>()),
            Times.Once);
        _otpServiceMock.Verify(
            service => service.SendOtpEmailAsync("user@zadana.com", "654321", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOtpGenerationFails_ShouldThrow()
    {
        SetupLocalizer();
        _identityAccountServiceMock
            .Setup(service => service.GeneratePasswordResetOtpAsync("blocked@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OtpDispatchResult(OtpDispatchStatus.Failed, Errors: ["provider_error"]));

        var handler = CreateHandler();

        var action = () => handler.Handle(new ForgotPasswordCommand("blocked@test.com"), CancellationToken.None);

        await action.Should().ThrowAsync<Exception>();
    }
}
