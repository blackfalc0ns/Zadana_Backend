using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Commands.RegisterDriver;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
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
    public async Task RegisterDriverCommandValidator_ShouldAllowOptionalCity()
    {
        var validator = new RegisterDriverCommandValidator(CreateLocalizer().Object);
        var command = CreateCommand(city: "");

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenOnlyEasternRegionProvided_ShouldStartPending()
    {
        await using var dbContext = CreateDbContext();
        var operationalRegion = new Domain.Modules.Geography.Entities.SaudiRegion(
            Guid.NewGuid(), "EASTERN", "المنطقة الشرقية", "Eastern Region", 26.4, 50.0, 6, 1);
        dbContext.SaudiRegions.Add(operationalRegion);
        await dbContext.SaveChangesAsync();
        var operationalCity = new Domain.Modules.Geography.Entities.SaudiCity(
            Guid.NewGuid(), operationalRegion.Id, "KHOBAR", "الخبر", "Al Khobar", 26.2, 50.2, 10, 1);
        dbContext.SaudiCities.Add(operationalCity);
        SeedActiveVendorInCity(dbContext, "EASTERN", "KHOBAR");
        await dbContext.SaveChangesAsync();

        var pending = new PendingRegistrationSnapshot(
            Guid.NewGuid(), "Ahmed Driver", "ahmed.driver@example.com", "+201001112233", UserRole.Driver, null);
        var pendingRegistrationService = new Mock<IPendingRegistrationService>();
        pendingRegistrationService
            .Setup(service => service.StartAsync(It.IsAny<StartPendingRegistrationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingRegistrationStartResult(
                PendingRegistrationStartStatus.Succeeded, pending, "1234", "reg-token"));
        var registrationWorkflow = new Mock<IRegistrationWorkflow>();
        registrationWorkflow
            .Setup(workflow => workflow.BuildPendingAuthResponse(pending, "reg-token", null))
            .Returns(new AuthResponseDto(null, null, IsVerified: false, RegistrationToken: "reg-token"));

        var handler = new RegisterDriverCommandHandler(
            pendingRegistrationService.Object,
            registrationWorkflow.Object,
            dbContext,
            Mock.Of<IOtpService>(),
            CreateLocalizer().Object);

        var result = await handler.Handle(CreateCommand(region: "EASTERN", city: null), CancellationToken.None);

        result.RegistrationToken.Should().Be("reg-token");
        pendingRegistrationService.Verify(
            service => service.StartAsync(
                It.Is<StartPendingRegistrationRequest>(request =>
                    request.PayloadJson.Contains("EASTERN") && !request.PayloadJson.Contains("DAMMAM")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRegionIsValid_ShouldStartPendingWithoutCreatingDriver()
    {
        await using var dbContext = CreateDbContext();

        var operationalRegion = new Domain.Modules.Geography.Entities.SaudiRegion(Guid.NewGuid(), "EASTERN", "Eastern", "Eastern", 26.4, 50.0, 6, 1);
        dbContext.SaudiRegions.Add(operationalRegion);
        await dbContext.SaveChangesAsync();

        var operationalCity = new Domain.Modules.Geography.Entities.SaudiCity(Guid.NewGuid(), operationalRegion.Id, "DAMMAM", "Dammam", "Dammam", 26.4, 50.0, 10, 1);
        dbContext.SaudiCities.Add(operationalCity);
        SeedActiveVendorInCity(dbContext, "EASTERN", "DAMMAM");
        await dbContext.SaveChangesAsync();

        var pending = new PendingRegistrationSnapshot(
            Guid.NewGuid(),
            "Ahmed Driver",
            "ahmed.driver@example.com",
            "+201001112233",
            UserRole.Driver,
            null);

        var pendingRegistrationService = new Mock<IPendingRegistrationService>();
        pendingRegistrationService
            .Setup(service => service.StartAsync(It.IsAny<StartPendingRegistrationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingRegistrationStartResult(
                PendingRegistrationStartStatus.Succeeded,
                pending,
                "1234",
                "reg-token"));

        var registrationWorkflow = new Mock<IRegistrationWorkflow>();
        registrationWorkflow
            .Setup(workflow => workflow.BuildPendingAuthResponse(pending, "reg-token", null))
            .Returns(new AuthResponseDto(null, null, IsVerified: false, RegistrationToken: "reg-token"));

        var otpService = new Mock<IOtpService>();
        var localizer = CreateLocalizer();

        var handler = new RegisterDriverCommandHandler(
            pendingRegistrationService.Object,
            registrationWorkflow.Object,
            dbContext,
            otpService.Object,
            localizer.Object);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        dbContext.Drivers.Local.Should().BeEmpty();
        result.IsVerified.Should().BeFalse();
        pendingRegistrationService.Verify(
            service => service.StartAsync(
                It.Is<StartPendingRegistrationRequest>(request =>
                    request.Role == UserRole.Driver &&
                    request.PayloadJson.Contains("DAMMAM")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        otpService.Verify(
            service => service.SendOtpEmailAsync(pending.Email, "1234", It.IsAny<CancellationToken>(), It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEasternRegionHasNoActiveVendor_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();

        dbContext.SaudiRegions.Add(
            new Domain.Modules.Geography.Entities.SaudiRegion(Guid.NewGuid(), "EASTERN", "Eastern", "Eastern", 26.4, 50.0, 6, 1));
        await dbContext.SaveChangesAsync();

        var pendingRegistrationService = new Mock<IPendingRegistrationService>();
        var registrationWorkflow = new Mock<IRegistrationWorkflow>();
        var handler = new RegisterDriverCommandHandler(
            pendingRegistrationService.Object,
            registrationWorkflow.Object,
            dbContext,
            Mock.Of<IOtpService>(),
            CreateLocalizer().Object);

        var action = () => handler.Handle(
            CreateCommand(region: "EASTERN", city: null),
            CancellationToken.None);

        await action.Should().ThrowAsync<BusinessRuleException>()
            .Where(exception => exception.ErrorCode == "DRIVER_REGION_HAS_NO_ACTIVE_VENDOR");
        pendingRegistrationService.Verify(
            service => service.StartAsync(It.IsAny<StartPendingRegistrationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static RegisterDriverCommand CreateCommand(
        string? region = null,
        string? city = null)
    {
        var resolvedRegion = region ?? "EASTERN";
        var resolvedCity = region is null && city is null ? "DAMMAM" : city;

        return new RegisterDriverCommand(
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
            resolvedRegion,
            resolvedCity,
            "https://cdn.example.com/driver/national-id-front.jpg",
            "https://cdn.example.com/driver/national-id-back.jpg",
            "https://cdn.example.com/driver/license.jpg",
            "https://cdn.example.com/driver/vehicle.jpg",
            "https://cdn.example.com/driver/photo.jpg");
    }

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
            .Setup(item => item[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        return localizer;
    }
}
