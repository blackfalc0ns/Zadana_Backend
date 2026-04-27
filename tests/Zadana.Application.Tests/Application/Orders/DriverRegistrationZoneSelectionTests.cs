using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Api.Modules.Delivery.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Modules.Delivery.Repositories;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverRegistrationZoneSelectionTests
{
    [Fact]
    public async Task RegisterDriverCommandValidator_ShouldRequirePrimaryZoneId()
    {
        var validator = new RegisterDriverCommandValidator(CreateLocalizer().Object);
        var command = CreateCommand(primaryZoneId: Guid.Empty);

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "PrimaryZoneId");
    }

    [Fact]
    public async Task Handle_WhenPrimaryZoneIsInactive_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();
        var inactiveZone = new DeliveryZone("Cairo", "Inactive Zone", 30.1m, 31.2m, 8m);
        inactiveZone.Deactivate();
        dbContext.DeliveryZones.Add(inactiveZone);
        await dbContext.SaveChangesAsync();

        var registrationWorkflow = new Mock<IRegistrationWorkflow>();
        var handler = new RegisterDriverCommandHandler(
            registrationWorkflow.Object,
            new DriverRepository(dbContext),
            Mock.Of<IUnitOfWork>(),
            dbContext);

        var action = () => handler.Handle(CreateCommand(inactiveZone.Id), CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "DELIVERY_ZONE_NOT_ACTIVE");
        registrationWorkflow.Verify(
            workflow => workflow.RegisterAccountAsync(It.IsAny<CreateIdentityAccountRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPrimaryZoneIsValid_ShouldAssignZoneAndReturnZoneInDriverStatus()
    {
        await using var dbContext = CreateDbContext();
        var zone = new DeliveryZone("Cairo", "Nasr City East", 30.0626m, 31.2497m, 8m);
        dbContext.DeliveryZones.Add(zone);
        await dbContext.SaveChangesAsync();

        var userSnapshot = new IdentityAccountSnapshot(
            Guid.NewGuid(),
            "Ahmed Driver",
            "ahmed.driver@example.com",
            "+201001112233",
            UserRole.Driver,
            AccountStatus.Pending,
            false,
            null,
            null,
            true,
            true);

        var registrationWorkflow = new Mock<IRegistrationWorkflow>();
        registrationWorkflow
            .Setup(workflow => workflow.RegisterAccountAsync(It.IsAny<CreateIdentityAccountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userSnapshot);
        registrationWorkflow
            .Setup(workflow => workflow.BuildAuthResponseAsync(
                userSnapshot,
                It.IsAny<DriverOperationalStatusDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityAccountSnapshot _, DriverOperationalStatusDto? driverStatus, CancellationToken _) =>
                new AuthResponseDto(null, null, IsVerified: false, DriverStatus: driverStatus));

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(workflow => workflow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new RegisterDriverCommandHandler(
            registrationWorkflow.Object,
            new DriverRepository(dbContext),
            unitOfWork.Object,
            dbContext);

        var result = await handler.Handle(CreateCommand(zone.Id), CancellationToken.None);

        var persistedDriver = dbContext.Drivers.Local.Should().ContainSingle().Subject;
        persistedDriver.PrimaryZoneId.Should().Be(zone.Id);
        result.DriverStatus.Should().NotBeNull();
        result.DriverStatus!.PrimaryZoneId.Should().Be(zone.Id);
        result.DriverStatus.ZoneName.Should().Be("Cairo - Nasr City East");
    }

    [Fact]
    public async Task GetPublicZones_ShouldReturnOkWithActiveZones()
    {
        var driverReadService = new Mock<IDriverReadService>();
        var expected = new[]
        {
            new DeliveryZoneDto(Guid.NewGuid(), "Cairo", "Nasr City East", 30.0626m, 31.2497m, 8m, true)
        };
        driverReadService
            .Setup(service => service.GetActiveZonesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new DriversController();

        var result = await controller.GetPublicZones(driverReadService.Object, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expected);
    }

    private static RegisterDriverCommand CreateCommand(Guid? primaryZoneId = null) =>
        new(
            "Ahmed Driver",
            "ahmed.driver@example.com",
            "+201001112233",
            "StrongPassword123!",
            DriverVehicleType.Motorcycle,
            "29801011234567",
            "CAI-DRV-4421",
            "Nasr City, Cairo",
            primaryZoneId ?? Guid.NewGuid(),
            "RIYADH",
            "RIYADH",
            "https://cdn.example.com/driver/national-id.jpg",
            "https://cdn.example.com/driver/license.jpg",
            "https://cdn.example.com/driver/vehicle.jpg",
            "https://cdn.example.com/driver/photo.jpg");

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static Mock<IStringLocalizer<SharedResource>> CreateLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<SharedResource>>();
        localizer
            .Setup(localizer => localizer[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        return localizer;
    }
}
