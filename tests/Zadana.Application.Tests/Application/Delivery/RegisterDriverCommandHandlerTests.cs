using FluentAssertions;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Delivery;

public class RegisterDriverCommandHandlerTests
{
    private readonly Mock<IPendingRegistrationService> _pendingRegistrationService = new();
    private readonly Mock<IRegistrationWorkflow> _registrationWorkflow = new();
    private readonly Mock<IApplicationDbContext> _dbContext = new();
    private readonly Mock<IOtpService> _otpService = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizer = new();

    private RegisterDriverCommandHandler CreateHandler()
    {
        _localizer
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        _localizer
            .Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] args) => new LocalizedString(key, key));

        return new RegisterDriverCommandHandler(
            _pendingRegistrationService.Object,
            _registrationWorkflow.Object,
            _dbContext.Object,
            _otpService.Object,
            _localizer.Object);
    }

    [Fact]
    public async Task Handle_WhenServiceAreaMissing_ShouldThrowBeforeCreatingPending()
    {
        var command = new RegisterDriverCommand(
            "Ahmed Driver",
            "driver@test.com",
            "01099999999",
            "P@ssword1",
            DriverVehicleType.Car,
            "12345678901234",
            "DL-12345",
            null,
            null,
            null,
            null,
            "Cairo, Egypt",
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var handler = CreateHandler();
        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "DRIVER_SERVICE_AREA_REQUIRED");

        _pendingRegistrationService.Verify(
            x => x.StartAsync(It.IsAny<StartPendingRegistrationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
