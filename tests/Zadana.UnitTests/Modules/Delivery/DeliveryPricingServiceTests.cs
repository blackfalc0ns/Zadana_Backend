using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Finances.Entities;
using Zadana.Domain.Modules.Identity.Entities;
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
            new DeliveryPricingCacheService(
                new MemoryCache(new MemoryCacheOptions()),
                new ServiceCollection()
                    .AddScoped<Zadana.Application.Common.Interfaces.IApplicationDbContext>(_ => context)
                    .BuildServiceProvider()
                    .GetRequiredService<IServiceScopeFactory>()));

        var quote = await service.QuoteAsync(branch.Id, address.Id, orderSubtotal: 1_000m);

        quote.BaseFee.Should().Be(20m);
        quote.DistanceFee.Should().BeGreaterThan(0m);
        quote.SurgeFee.Should().Be(0m);
        quote.TotalFee.Should().Be(quote.BaseFee + quote.DistanceFee + quote.SurgeFee);
        quote.DriverToVendorFee.Should().Be(10m);
        quote.VendorToCustomerFee.Should().BeGreaterThan(10m);
        quote.HasAnomalyWarning.Should().BeFalse();
    }
}
