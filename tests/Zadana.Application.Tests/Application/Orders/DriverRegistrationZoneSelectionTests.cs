using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
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

        var operationalRegion = new Domain.Modules.Geography.Entities.SaudiRegion(Guid.NewGuid(), "EASTERN", "Eastern", "Eastern", 26.4, 50.0, 6, 1);
        dbContext.SaudiRegions.Add(operationalRegion);
        await dbContext.SaveChangesAsync();

        var operationalCity = new Domain.Modules.Geography.Entities.SaudiCity(Guid.NewGuid(), operationalRegion.Id, "DAMMAM", "Dammam", "Dammam", 26.4, 50.0, 10, 1);
        dbContext.SaudiCities.Add(operationalCity);
        SeedActiveVendorInCity(dbContext, "EASTERN", "DAMMAM");
        await dbContext.SaveChangesAsync();

        var userSnapshot = new IdentityAccountSnapshot(
            Guid.NewGuid(),
            "Ahmed Driver",
            "ahmed.driver@example.com",
            "+201001112233",
            UserRole.Driver,
            0,
            AccountStatus.Pending,
            false,
            null,
            null,
            true,
            true,
            false);

        var registrationWorkflow = new Mock<IRegistrationWorkflow>();
        registrationWorkflow
            .Setup(workflow => workflow.RegisterAccountAsync(It.IsAny<CreateIdentityAccountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userSnapshot);
        registrationWorkflow
            .Setup(workflow => workflow.GenerateRegistrationOtpAsync(userSnapshot, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationOtpDispatch(userSnapshot, "123456"));
        registrationWorkflow
            .Setup(workflow => workflow.DispatchRegistrationOtpEmailAsync(
                userSnapshot.Email!,
                "123456",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
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
            Mock.Of<IIdentityAccountService>(),
            new DriverRepository(dbContext),
            unitOfWork.Object,
            dbContext,
            Mock.Of<IAdminAlertService>(),
            Mock.Of<ILogger<RegisterDriverCommandHandler>>());

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        var persistedDriver = dbContext.Drivers.Local.Should().ContainSingle().Subject;
        persistedDriver.Region.Should().Be("EASTERN");
        persistedDriver.City.Should().Be("DAMMAM");
        result.DriverStatus.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenCityDoesNotBelongToRegion_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();

        // Seed geography — city belongs to a different region
        var region = new Domain.Modules.Geography.Entities.SaudiRegion(Guid.NewGuid(), "RIYADH", "الرياض", "Riyadh", 24.7, 46.7, 6, 1);
        var otherRegion = new Domain.Modules.Geography.Entities.SaudiRegion(Guid.NewGuid(), "MAKKAH", "مكة", "Makkah", 21.4, 39.8, 6, 2);
        dbContext.SaudiRegions.AddRange(
            region,
            otherRegion,
            new Domain.Modules.Geography.Entities.SaudiRegion(Guid.NewGuid(), "EASTERN", "Eastern", "Eastern", 26.4, 50.0, 6, 3));
        await dbContext.SaveChangesAsync();

        var city = new Domain.Modules.Geography.Entities.SaudiCity(Guid.NewGuid(), otherRegion.Id, "JEDDAH", "جدة", "Jeddah", 21.5, 39.2, 10, 1);
        dbContext.SaudiCities.Add(city);
        await dbContext.SaveChangesAsync();

        var registrationWorkflow = new Mock<IRegistrationWorkflow>();
        var handler = new RegisterDriverCommandHandler(
            registrationWorkflow.Object,
            Mock.Of<IIdentityAccountService>(),
            new DriverRepository(dbContext),
            Mock.Of<IUnitOfWork>(),
            dbContext,
            Mock.Of<IAdminAlertService>(),
            Mock.Of<ILogger<RegisterDriverCommandHandler>>());

        var action = () => handler.Handle(
            CreateCommand(region: "EASTERN", city: "JEDDAH"),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "UNSUPPORTED_OPERATIONAL_CITY");
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
            DateTime.UtcNow.Date.AddYears(1),
            DateTime.UtcNow.Date.AddYears(1),
            "VEH-4421",
            DateTime.UtcNow.Date.AddYears(1),
            "Nasr City, Cairo",
            region ?? "EASTERN",
            city ?? "DAMMAM",
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

    private static void SeedActiveVendorInCity(ApplicationDbContext dbContext, string region, string city)
    {
        var vendorUser = new User(
            "Active Vendor",
            $"active-vendor-{Guid.NewGuid():N}@example.com",
            "+201009998877",
            UserRole.Vendor);

        var vendor = new Vendor(
            vendorUser.Id,
            "Active Vendor",
            "Active Vendor",
            "Grocery",
            $"CR{Guid.NewGuid():N}"[..12],
            "active-vendor@example.com",
            "+201009998877",
            region: region,
            city: city);

        vendor.Approve(10m, Guid.NewGuid());

        var branch = new VendorBranch(
            vendor.Id,
            "Main Branch",
            "MAIN",
            true,
            "Dammam branch address",
            region,
            city,
            26.4m,
            50.0m,
            "+201009998877",
            "Manager",
            "+201009998877",
            5m);

        dbContext.Users.Add(vendorUser);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorBranches.Add(branch);
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
