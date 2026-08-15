using FluentAssertions;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.RegisterCustomer;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Identity;

public class RegisterCustomerCommandHandlerTests
{
    private readonly Mock<IPendingRegistrationService> _pendingRegistrationService = new();
    private readonly Mock<IRegistrationWorkflow> _registrationWorkflow = new();
    private readonly Mock<IOtpService> _otpService = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizer = new();

    private RegisterCustomerCommandHandler CreateHandler()
    {
        _localizer
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        _localizer
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, key));

        return new RegisterCustomerCommandHandler(
            _pendingRegistrationService.Object,
            _registrationWorkflow.Object,
            _otpService.Object,
            _localizer.Object);
    }

    private static RegisterCustomerCommand CreateCommand(string email = "ahmed@test.com") =>
        new("Ahmed Ali", email, "01011122233", "P@ssword1", null, "Address Line", "Home", "123", "1", "1A", "City", "Area", 30.0m, 31.0m);

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ShouldThrowBusinessRuleException()
    {
        _pendingRegistrationService
            .Setup(x => x.StartAsync(It.IsAny<StartPendingRegistrationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingRegistrationStartResult(PendingRegistrationStartStatus.DuplicateEmailOrPhone));

        var handler = CreateHandler();
        var act = () => handler.Handle(CreateCommand("taken@mail.com"), CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "USER_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldCreatePendingAndSendOtpWithoutAspNetUser()
    {
        var pendingId = Guid.NewGuid();
        var pending = new PendingRegistrationSnapshot(
            pendingId,
            "Ahmed Ali",
            "ahmed@test.com",
            "01011122233",
            UserRole.Customer,
            null);

        _pendingRegistrationService
            .Setup(x => x.StartAsync(It.IsAny<StartPendingRegistrationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingRegistrationStartResult(
                PendingRegistrationStartStatus.Succeeded,
                pending,
                "1234",
                "reg-token"));

        _registrationWorkflow
            .Setup(x => x.BuildPendingAuthResponse(pending, "reg-token", null))
            .Returns(new AuthResponseDto(
                null,
                new CurrentUserDto(pendingId, "Ahmed Ali", "ahmed@test.com", "01011122233", "Customer", false),
                false,
                RegistrationToken: "reg-token"));

        var handler = CreateHandler();
        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.IsVerified.Should().BeFalse();
        result.Tokens.Should().BeNull();
        result.User!.Email.Should().Be("ahmed@test.com");
        result.User.Id.Should().Be(pendingId);

        _otpService.Verify(
            o => o.SendOtpEmailAsync("ahmed@test.com", "1234", It.IsAny<CancellationToken>(), It.IsAny<int>()),
            Times.Once);

        _pendingRegistrationService.Verify(
            x => x.StartAsync(
                It.Is<StartPendingRegistrationRequest>(r =>
                    r.Email == "ahmed@test.com" &&
                    r.Role == UserRole.Customer &&
                    !string.IsNullOrWhiteSpace(r.PayloadJson)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLinkingExistingAccount_ShouldSendOtpToAccountEmail()
    {
        var pendingId = Guid.NewGuid();
        var pending = new PendingRegistrationSnapshot(
            pendingId,
            "Ahmed Ali",
            "new-customer@test.com",
            "01011122233",
            UserRole.Customer,
            null,
            Guid.NewGuid(),
            "driver@test.com");

        _pendingRegistrationService
            .Setup(x => x.StartAsync(It.IsAny<StartPendingRegistrationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingRegistrationStartResult(
                PendingRegistrationStartStatus.Succeeded,
                pending,
                "1234",
                "reg-token"));

        _registrationWorkflow
            .Setup(x => x.BuildPendingAuthResponse(pending, "reg-token", null))
            .Returns(new AuthResponseDto(
                null,
                new CurrentUserDto(pendingId, "Ahmed Ali", "new-customer@test.com", "01011122233", "Customer", false),
                false,
                RegistrationToken: "reg-token"));

        var handler = CreateHandler();
        await handler.Handle(CreateCommand("new-customer@test.com"), CancellationToken.None);

        _otpService.Verify(
            o => o.SendOtpEmailAsync("driver@test.com", "1234", It.IsAny<CancellationToken>(), It.IsAny<int>()),
            Times.Once);
        _otpService.Verify(
            o => o.SendOtpEmailAsync("new-customer@test.com", It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>()),
            Times.Never);
    }
}
