using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography;
using Zadana.Domain.Modules.Geography.Entities;
using Zadana.Infrastructure.Modules.Geography.Services;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Application.Tests.Application.Geography;

public class GeographyCityResolverTests
{
    [Theory]
    [InlineData("الرياض", "RIYADH")]
    [InlineData("RIYADH", "RIYADH")]
    [InlineData("riyadh", "RIYADH")]
    [InlineData("Riyadh", "RIYADH")]
    [InlineData("جدة", "JEDDAH")]
    [InlineData("JEDDAH", "JEDDAH")]
    [InlineData("الدمام", "DAMMAM")]
    public void Resolve_KnownCityVariants_ShouldMapToOfficialCode(string rawCity, string expectedCode)
    {
        var resolver = CreateResolver();
        var resolved = resolver.Resolve(rawCity);

        resolved.CityCode.Should().Be(expectedCode);
        resolved.IsKnown.Should().BeTrue();
    }

    [Fact]
    public void ResolveLocation_WithArabicCity_ShouldMapToOfficialCode()
    {
        var resolver = CreateResolver();
        var resolved = resolver.ResolveLocation("الدمام", "EASTERN");

        resolved.CityCode.Should().Be("DAMMAM");
        resolved.IsKnown.Should().BeTrue();
    }

    [Fact]
    public void Resolve_UnknownCity_ShouldReturnUnmapped()
    {
        var resolver = CreateResolver();
        var resolved = resolver.Resolve("مدينة وهمية");

        resolved.CityCode.Should().Be(GeographyCoverageConstants.UnmappedCityCode);
        resolved.MatchQuality.Should().Be(GeographyCityMatchQuality.Unknown);
    }

    [Theory]
    [InlineData("الرياض", "riyadh")]
    [InlineData("RIYADH", "riyadh")]
    public void NormalizeCityName_ShouldProduceStableKeys(string input, string expected)
    {
        GeographyCityNormalization.NormalizeCityName(input).Should().Be(expected);
    }

    private static GeographyCityResolver CreateResolver()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddSingleton<GeographyCityResolver>();
        services.AddSingleton<IGeographyCityResolver>(provider => provider.GetRequiredService<GeographyCityResolver>());
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SeedGeography(db);

        var resolver = serviceProvider.GetRequiredService<GeographyCityResolver>();
        resolver.RefreshCatalogAsync().GetAwaiter().GetResult();
        return resolver;
    }

    private static void SeedGeography(ApplicationDbContext db)
    {
        var regionId = Guid.NewGuid();
        db.SaudiRegions.Add(new SaudiRegion(regionId, "RIYADH", "منطقة الرياض", "Riyadh Region", 24.7, 46.7, 8, 1));
        db.SaudiCities.Add(new SaudiCity(Guid.NewGuid(), regionId, "RIYADH", "الرياض", "Riyadh", 24.7, 46.7, 12, 1));
        db.SaudiCities.Add(new SaudiCity(Guid.NewGuid(), regionId, "JEDDAH", "جدة", "Jeddah", 21.5, 39.1, 12, 2));
        db.SaudiCities.Add(new SaudiCity(Guid.NewGuid(), regionId, "DAMMAM", "الدمام", "Dammam", 26.4, 50.0, 12, 3));
        db.SaveChanges();
    }
}
