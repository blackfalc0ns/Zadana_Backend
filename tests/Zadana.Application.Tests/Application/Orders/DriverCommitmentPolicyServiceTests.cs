using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Infrastructure.Modules.Delivery.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverCommitmentPolicyServiceTests
{
    [Fact]
    public async Task GetDriverSummaryAsync_ShouldCountRejectedAndTimedOutWithinRollingWindows()
    {
        await using var dbContext = CreateDbContext();
        var driverId = Guid.NewGuid();

        var recentRejected = new DeliveryOfferAttempt(Guid.NewGuid(), null, driverId, 1, DateTime.UtcNow.AddMinutes(1));
        recentRejected.MarkRejected("busy");
        var recentTimedOut = new DeliveryOfferAttempt(Guid.NewGuid(), null, driverId, 2, DateTime.UtcNow.AddMinutes(1));
        recentTimedOut.MarkTimedOut();
        var accepted = new DeliveryOfferAttempt(Guid.NewGuid(), null, driverId, 3, DateTime.UtcNow.AddMinutes(1));
        accepted.MarkAccepted();

        dbContext.DeliveryOfferAttempts.AddRange(recentRejected, recentTimedOut, accepted);
        await dbContext.SaveChangesAsync();

        var service = new DriverCommitmentPolicyService(dbContext, dbContext);

        var summary = await service.GetDriverSummaryAsync(driverId, CancellationToken.None);

        summary.RejectedOffers.Should().Be(1);
        summary.TimedOutOffers.Should().Be(1);
        summary.AcceptedOffers.Should().Be(1);
        summary.DailyRejections.Should().Be(2);
        summary.WeeklyRejections.Should().Be(2);
        summary.CommitmentScore.Should().Be(74m);
        summary.EnforcementLevel.Should().Be(DriverCommitmentEnforcementLevel.Watch.ToString());
    }

    [Fact]
    public async Task GetDriverSummaryAsync_WhenDailyLimitReached_ShouldSoftBlockDriver()
    {
        await using var dbContext = CreateDbContext();
        var driverId = Guid.NewGuid();

        for (var index = 0; index < 20; index++)
        {
            var attempt = new DeliveryOfferAttempt(Guid.NewGuid(), null, driverId, index + 1, DateTime.UtcNow.AddMinutes(1));
            attempt.MarkRejected("skip");
            dbContext.DeliveryOfferAttempts.Add(attempt);
        }

        await dbContext.SaveChangesAsync();

        var service = new DriverCommitmentPolicyService(dbContext, dbContext);
        var summary = await service.GetDriverSummaryAsync(driverId, CancellationToken.None);

        summary.DailyRejections.Should().Be(20);
        summary.CanReceiveOffers.Should().BeFalse();
        summary.EnforcementLevel.Should().Be(DriverCommitmentEnforcementLevel.SoftBlocked.ToString());
        summary.RestrictionMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ApplyOperationalEnforcementAsync_WhenDriverSoftBlockedTwiceInWeek_ShouldCreateIncidentAndDisableAvailability()
    {
        await using var dbContext = CreateDbContext();
        var driver = new Driver(Guid.NewGuid(), DriverVehicleType.Car, "1234567890", "DRV-COMMIT-1");
        driver.Approve(Guid.NewGuid());
        driver.ToggleAvailability(true);
        dbContext.Drivers.Add(driver);

        for (var index = 0; index < 20; index++)
        {
            AddHistoricalRejectedAttempt(dbContext, driver.Id, DateTime.UtcNow.AddDays(-1).Date.AddHours(1).AddMinutes(index));
            AddHistoricalRejectedAttempt(dbContext, driver.Id, DateTime.UtcNow.Date.AddHours(1).AddMinutes(index));
        }

        await dbContext.SaveChangesAsync();

        var service = new DriverCommitmentPolicyService(dbContext, dbContext);
        await service.ApplyOperationalEnforcementAsync([driver.Id], CancellationToken.None);

        driver.IsAvailable.Should().BeFalse();
        dbContext.DriverIncidents.Should().ContainSingle();
        dbContext.DriverIncidents.Single().IncidentType.Should().Be("offer-compliance");
        dbContext.DriverIncidents.Single().Severity.Should().Be(DriverIncidentSeverity.High);
    }

    private static void AddHistoricalRejectedAttempt(ApplicationDbContext dbContext, Guid driverId, DateTime respondedAtUtc)
    {
        var attempt = new DeliveryOfferAttempt(Guid.NewGuid(), null, driverId, dbContext.DeliveryOfferAttempts.Count() + 1, respondedAtUtc.AddMinutes(1));
        attempt.MarkRejected("skip");
        typeof(DeliveryOfferAttempt).GetProperty(nameof(DeliveryOfferAttempt.RespondedAtUtc))!
            .SetValue(attempt, respondedAtUtc);
        typeof(DeliveryOfferAttempt).GetProperty(nameof(DeliveryOfferAttempt.OfferedAtUtc))!
            .SetValue(attempt, respondedAtUtc.AddSeconds(-20));
        dbContext.DeliveryOfferAttempts.Add(attempt);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
