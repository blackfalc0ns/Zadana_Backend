using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Modules.Delivery.Repositories;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverRegistrationRegionCityTests
{
    [Fact]
    public async Task RegisterDriverCommandValidator_ShouldRequireRegion()
    {
        var validator = new RegisterDriverCommandValidator(CreateLocalizer().Object);
        var command = CreateCommand(region: "");

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Region");
    }

    [Fact]
    public async Task RegisterDriverCommandValidator_ShouldRequireCity()
    {
        var validator = new RegisterDriverCommandValidator(CreateLocalizer().Object);
        var command = CreateCommand(city: "");

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "City");
    }

    [Fact]
    public async Task Handle_WhenRegionIsValid_ShouldPersistDriverWithRegionCity()
    {
        await using var dbContext = CreateDbContext();

        // Seed geography
        var region = new Domain.Modules.Geography.Entities.SaudiRegion(Guid.NewGuid(), "RIYADH", "الرياض", "Riyadh", 24.7, 46.7, 6, 1);
        dbContext.SaudiRegions.Add(region);
        await dbContext.SaveChangesAsync();

        var city = new Domain.Modules.Geography.Entities.SaudiCity(Guid.NewGuid(), region.Id, "RIYADH", "الرياض", "Riyadh", 24.7, 46.7, 10, 1);
        dbContext.SaudiCities.Add(city);
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

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        var persistedDriver = dbContext.Drivers.Local.Should().ContainSingle().Subject;
        persistedDriver.Region.Should().Be("RIYADH");
        persistedDriver.City.Should().Be("RIYADH");
        result.DriverStatus.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenCityDoesNotBelongToRegion_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();

        // Seed geography — city belongs to a different region
        var region = new Domain.Modules.Geography.Entities.SaudiRegion(Guid.NewGuid(), "RIYADH", "الرياض", "Riyadh", 24.7, 46.7, 6, 1);
        var otherRegion = new Domain.Modules.Geography.Entities.SaudiRegion(Guid.NewGuid(), "MAKKAH", "مكة", "Makkah", 21.4, 39.8, 6, 2);
        dbContext.SaudiRegions.AddRange(region, otherRegion);
        await dbContext.SaveChangesAsync();

        var city = new Domain.Modules.Geography.Entities.SaudiCity(Guid.NewGuid(), otherRegion.Id, "JEDDAH", "جدة", "Jeddah", 21.5, 39.2, 10, 1);
        dbContext.SaudiCities.Add(city);
        await dbContext.SaveChangesAsync();

        var registrationWorkflow = new Mock<IRegistrationWorkflow>();
        var handler = new RegisterDriverCommandHandler(
            registrationWorkflow.Object,
            new DriverRepository(dbContext),
            Mock.Of<IUnitOfWork>(),
            dbContext);

        var action = () => handler.Handle(
            CreateCommand(region: "RIYADH", city: "JEDDAH"),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "INVALID_CITY");
        registrationWorkflow.Verify(
            workflow => workflow.RegisterAccountAsync(It.IsAny<CreateIdentityAccountRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static RegisterDriverCommand CreateCommand(
        string? region = null,
        string? city = null) =>
        new(
            "Ahmed Driver",
            "ahmed.driver@example.com",
            "+201001112233",
            "StrongPassword123!",
            DriverVehicleType.Motorcycle,
            "29801011234567",
            "CAI-DRV-4421",
            "Nasr City, Cairo",
            region ?? "RIYADH",
            city ?? "RIYADH",
            "https://cdn.example.com/driver/national-id-front.jpg",
            "https://cdn.example.com/driver/national-id-back.jpg",
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
