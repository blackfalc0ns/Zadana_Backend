using FluentAssertions;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Modules.Delivery.Services;

namespace Zadana.Application.Tests.Application.Orders;

public class DeliveryDispatchScoringTests
{
    [Fact]
    public void EvaluateCandidate_ShouldPreferCloserDriverOverSameCityLabel()
    {
        var now = DateTime.UtcNow;
        var context = new DeliveryDispatchContext(
            PickupZone: null,
            PickupCity: "DAMMAM",
            PickupRegion: "EASTERN",
            PickupLatitude: 26.3927m,
            PickupLongitude: 49.9777m);

        var closeDriver = CreateDriver(region: "EASTERN", city: "KHOBAR");
        var farDriver = CreateDriver(region: "EASTERN", city: "DAMMAM");
        var closeEval = DeliveryDispatchScoring.EvaluateCandidate(
            closeDriver,
            new DriverLocation(closeDriver.Id, 26.40m, 50.00m, 10m),
            activeTaskCount: 0,
            reliabilityScore: 50m,
            commitmentScore: 100m,
            context,
            now);
        var farEval = DeliveryDispatchScoring.EvaluateCandidate(
            farDriver,
            new DriverLocation(farDriver.Id, 26.20m, 50.20m, 10m),
            0,
            50m,
            100m,
            context,
            now);

        closeEval.CompositeScore.Should().BeLessThan(farEval.CompositeScore);
        closeEval.MatchReason.Should().Be("pickup-live-gps");
    }

    private static Driver CreateDriver(string region, string city)
    {
        var user = new User("Scoring Driver", "scoring.driver@test.com", "01000000997", UserRole.Driver);
        var driver = new Driver(user.Id, DriverVehicleType.Car, "1234567899", "DRV-SCORE-1", region: region, city: city);
        driver.Approve(Guid.NewGuid());
        return driver;
    }
}
