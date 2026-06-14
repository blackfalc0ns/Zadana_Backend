using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Finances.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Modules.Delivery.Services;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Delivery;

public class DeliveryPricingServiceTests
{
    [Fact]
    public async Task QuoteAsync_ShouldReturnBaseDistanceAndSurgeAsPricingComponents()
    {
        await using var context = TestDbContextFactory.Create();
        var vendor = new Vendor(
            Guid.NewGuid(),
            "Store Arabic",
            "Store",
            "grocery",
            $"CR-{Guid.NewGuid():N}"[..12],
            "vendor.pricing@test.com",
            "01000000999",
            city: "Riyadh");
        var branch = new VendorBranch(
            vendor.Id,
            "Main",
            "Riyadh",
            24.7136m,
            46.6753m,
            "01000000999",
            100m);
        var address = new CustomerAddress(
            Guid.NewGuid(),
            "Customer",
            "01000000111",
            "Riyadh",
            city: "Riyadh",
            latitude: 24.7743m,
            longitude: 46.7386m);

        context.Vendors.Add(vendor);
        context.VendorBranches.Add(branch);
        context.CustomerAddresses.Add(address);
        context.DeliveryPricingDefaults.Add(new DeliveryPricingDefaults(
            Guid.NewGuid(),
            baseDeliveryFee: 10m,
            includedKm: 0m,
            extraKmFee: 1m,
            minDeliveryFee: 0m,
            maxDeliveryFee: 0m,
            isPricingActive: true,
            vatPercent: 15m,
            codFeeType: "flat",
            codFlatFee: 0m,
            codPercent: 0m,
            isVatActive: true,
            isCodFeeActive: false,
            minTotalDeliveryFee: 0m,
            maxTotalDeliveryFee: 0m,
            maxQuotedDistanceKm: 0m,
            warningSubtotalRatioThreshold: 0.1m));
        await context.SaveChangesAsync();

        var service = new DeliveryPricingService(
            context,
            Mock.Of<IDriverCommitmentPolicyService>(),
            CreatePricingCache(context));

        var quote = await service.QuoteAsync(branch.Id, address.Id, orderSubtotal: 1_000m);

        quote.BaseFee.Should().Be(20m);
        quote.DistanceFee.Should().BeGreaterThan(0m);
        quote.SurgeFee.Should().Be(0m);
        quote.TotalFee.Should().Be(quote.BaseFee + quote.DistanceFee + quote.SurgeFee);
        quote.DriverToVendorFee.Should().Be(10m);
        quote.VendorToCustomerFee.Should().BeGreaterThan(10m);
        quote.HasAnomalyWarning.Should().BeFalse();
    }

    [Fact]
    public async Task QuoteAsync_ShouldUseOnlyLiveDriversFromBranchCityForDriverOrigin()
    {
        await using var context = TestDbContextFactory.Create();
        var vendor = new Vendor(
            Guid.NewGuid(),
            "Store Arabic",
            "Store",
            "grocery",
            $"CR-{Guid.NewGuid():N}"[..12],
            "vendor.branch-city-pricing@test.com",
            "01000000888",
            region: "EASTERN",
            city: "DAMMAM");
        var branch = new VendorBranch(
            vendor.Id,
            "Dhahran Branch",
            "DHAHRAN",
            true,
            "Dhahran pickup",
            "EASTERN",
            "DHAHRAN",
            26.2361m,
            50.0393m,
            "01000000888",
            "Manager",
            "01000000889",
            100m);
        var address = new CustomerAddress(
            Guid.NewGuid(),
            "Customer",
            "01000000111",
            "Dhahran",
            city: "DHAHRAN",
            latitude: 26.2500m,
            longitude: 50.0500m);

        var wrongCityUser = new User("Dammam Driver", "dammam.driver@test.com", "01000000701", UserRole.Driver);
        var sameCityUser = new User("Dhahran Driver", "dhahran.driver@test.com", "01000000702", UserRole.Driver);
        var wrongCityDriver = CreateApprovedAvailableDriver(wrongCityUser.Id, "EASTERN", "DAMMAM", "DRV-DAMMAM");
        var sameCityDriver = CreateApprovedAvailableDriver(sameCityUser.Id, "EASTERN", "DHAHRAN", "DRV-DHAHRAN");

        context.Users.AddRange(wrongCityUser, sameCityUser);
        context.Vendors.Add(vendor);
        context.VendorBranches.Add(branch);
        context.CustomerAddresses.Add(address);
        context.Drivers.AddRange(wrongCityDriver, sameCityDriver);
        context.DriverLocations.Add(new DriverLocation(wrongCityDriver.Id, 26.2361m, 50.0393m, 5m));
        context.DriverLocations.Add(new DriverLocation(sameCityDriver.Id, 26.3000m, 50.0900m, 5m));
        context.DeliveryPricingDefaults.Add(new DeliveryPricingDefaults(
            Guid.NewGuid(),
            baseDeliveryFee: 10m,
            includedKm: 0m,
            extraKmFee: 1m,
            minDeliveryFee: 0m,
            maxDeliveryFee: 0m,
            isPricingActive: true,
            vatPercent: 15m,
            codFeeType: "flat",
            codFlatFee: 0m,
            codPercent: 0m,
            isVatActive: true,
            isCodFeeActive: false,
            minTotalDeliveryFee: 0m,
            maxTotalDeliveryFee: 0m,
            maxQuotedDistanceKm: 0m,
            warningSubtotalRatioThreshold: 0.1m));
        await context.SaveChangesAsync();

        var commitmentPolicy = new Mock<IDriverCommitmentPolicyService>();
        commitmentPolicy
            .Setup(service => service.ApplyOperationalEnforcementAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        commitmentPolicy
            .Setup(service => service.GetDriverSummariesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                ids.ToDictionary(
                    id => id,
                    _ => new DriverCommitmentSummaryDto(0, 0, 0, 0, 0, 100m, "Healthy", true, null, null, null)));

        var service = new DeliveryPricingService(
            context,
            commitmentPolicy.Object,
            CreatePricingCache(context));

        var quote = await service.QuoteAsync(branch.Id, address.Id, orderSubtotal: 1_000m);

        quote.PricingOriginType.Should().Be("live_driver");
        quote.PricingOriginDriverId.Should().Be(sameCityDriver.Id);
    }

    private static Driver CreateApprovedAvailableDriver(Guid userId, string region, string city, string licenseNumber)
    {
        var driver = new Driver(
            userId,
            DriverVehicleType.Car,
            nationalId: licenseNumber.Replace("DRV-", "1").PadRight(10, '0')[..10],
            licenseNumber: licenseNumber,
            nationalIdExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            driverLicenseExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            vehicleLicenseNumber: $"VEH-{licenseNumber}",
            vehicleLicenseExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            address: city,
            nationalIdFrontImageUrl: "https://cdn.example.com/id-front.jpg",
            nationalIdBackImageUrl: "https://cdn.example.com/id-back.jpg",
            licenseImageUrl: "https://cdn.example.com/license.jpg",
            vehicleImageUrl: "https://cdn.example.com/vehicle.jpg",
            personalPhotoUrl: "https://cdn.example.com/photo.jpg",
            region: region,
            city: city);

        driver.Approve(Guid.NewGuid());
        driver.ToggleAvailability(true);
        return driver;
    }

    private static DeliveryPricingCacheService CreatePricingCache(IApplicationDbContext context) =>
        new(new MemoryCache(new MemoryCacheOptions()), new NonDisposingScopeFactory(context));

    private sealed class NonDisposingScopeFactory(IApplicationDbContext context) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new NonDisposingScope(context);
    }

    private sealed class NonDisposingScope(IApplicationDbContext context) : IServiceScope, IServiceProvider
    {
        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IApplicationDbContext) ? context : null;

        public void Dispose()
        {
        }
    }
}
