using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Delivery.Commands.UpdateDriverAvailability;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Modules.Delivery.Repositories;
using Zadana.Infrastructure.Modules.Delivery.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Finances.Services;

namespace Zadana.Application.Tests.Application.Orders;

public class UpdateDriverAvailabilityCommandHandlerTests
{
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<IOneSignalPushService> _oneSignalPushServiceMock = new();

    public UpdateDriverAvailabilityCommandHandlerTests()
    {
        _notificationServiceMock
            .Setup(service => service.SendToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<NotificationDispatchRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _notificationServiceMock
            .Setup(service => service.SendDriverHomeUpdatedAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _oneSignalPushServiceMock
            .Setup(service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(true, true, false, 200, "mock-id", null));
    }

    [Fact]
    public async Task Handle_WhenDriverIsSoftBlocked_ShouldRejectAvailabilityEnable()
    {
        await using var dbContext = CreateDbContext();
        var driverUser = new User("Availability Driver", "availability.driver@test.com", "01000000177", UserRole.Driver);
        var driver = new Driver(
            driverUser.Id,
            DriverVehicleType.Car,
            "1234567890",
            "DRV-AVAIL-1",
            region: "EASTERN",
            city: "DAMMAM");
        driver.Approve(Guid.NewGuid());
        dbContext.Users.Add(driverUser);
        dbContext.Drivers.Add(driver);

        for (var index = 0; index < 20; index++)
        {
            var attempt = new DeliveryOfferAttempt(Guid.NewGuid(), null, driver.Id, index + 1, DateTime.UtcNow.AddMinutes(1));
            attempt.MarkRejected("busy");
            dbContext.DeliveryOfferAttempts.Add(attempt);
        }

        await dbContext.SaveChangesAsync();

        var handler = new UpdateDriverAvailabilityCommandHandler(
            new DriverRepository(dbContext),
            new DriverCommitmentPolicyService(dbContext, dbContext),
            dbContext,
            _notificationServiceMock.Object,
            _oneSignalPushServiceMock.Object,
            CreateCodEnforcementService(dbContext));

        var act = async () => await handler.Handle(
            new UpdateDriverAvailabilityCommand(driver.UserId, true),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.ErrorCode.Should().Be("DRIVER_SOFT_BLOCKED_BY_REJECTIONS");
    }

    [Fact]
    public async Task Handle_WhenRequiredDocumentExpired_ShouldLockDriverAndRejectAvailabilityEnable()
    {
        await using var dbContext = CreateDbContext();
        var driverUser = new User("Expired Availability Driver", "expired.availability.driver@test.com", "01000000178", UserRole.Driver);
        var driver = new Driver(
            driverUser.Id,
            DriverVehicleType.Car,
            "1234567890",
            "DRV-AVAIL-2",
            nationalIdExpiryDate: DateTime.UtcNow.Date.AddDays(-1),
            driverLicenseExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            vehicleLicenseNumber: "VEH-AVAIL-2",
            vehicleLicenseExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            address: "Cairo",
            nationalIdFrontImageUrl: "https://cdn.example.com/id-front.jpg",
            nationalIdBackImageUrl: "https://cdn.example.com/id-back.jpg",
            licenseImageUrl: "https://cdn.example.com/license.jpg",
            vehicleImageUrl: "https://cdn.example.com/vehicle.jpg",
            personalPhotoUrl: "https://cdn.example.com/photo.jpg");
        driver.Approve(Guid.NewGuid());

        dbContext.Users.Add(driverUser);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateDriverAvailabilityCommandHandler(
            new DriverRepository(dbContext),
            new DriverCommitmentPolicyService(dbContext, dbContext),
            dbContext,
            _notificationServiceMock.Object,
            _oneSignalPushServiceMock.Object,
            CreateCodEnforcementService(dbContext));

        var act = async () => await handler.Handle(
            new UpdateDriverAvailabilityCommand(driver.UserId, true),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.ErrorCode.Should().Be("DRIVER_NOT_READY_FOR_DISPATCH");

        driver.Status.Should().Be(AccountStatus.Inactive);
        driver.VerificationStatus.Should().Be(DriverVerificationStatus.NeedsDocuments);
        driver.IsAvailable.Should().BeFalse();

        _notificationServiceMock.Verify(service => service.SendToUserAsync(
                driver.UserId,
                It.IsAny<NotificationDispatchRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _notificationServiceMock.Verify(service => service.SendDriverHomeUpdatedAsync(
                driver.UserId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _oneSignalPushServiceMock.Verify(service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(request =>
                    request.ExternalUserId == driver.UserId.ToString() &&
                    request.Type == "driver_account_updated" &&
                    request.TargetApplication == OneSignalApplicationTarget.Driver),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static DriverCodEnforcementService CreateCodEnforcementService(ApplicationDbContext dbContext) =>
        new(dbContext, Options.Create(new FinancialSettingsOptions()));
}
