using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Delivery.Commands.UpdateDriverLocation;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

public class UpdateDriverLocationCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPersistAccuracyMetersWhenProvided()
    {
        await using var dbContext = CreateDbContext();
        var driverUser = new User("Geo Driver", "geo.driver@test.com", "01000000991", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567890", "DRV-GEO-1");
        driver.Approve(Guid.NewGuid());

        dbContext.Users.Add(driverUser);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateDriverLocationCommandHandler(dbContext, dbContext);

        await handler.Handle(
            new UpdateDriverLocationCommand(driver.Id, 24.7136m, 46.6753m, 14.5m),
            CancellationToken.None);

        var location = await dbContext.DriverLocations.SingleAsync();
        location.AccuracyMeters.Should().Be(14.5m);
        location.Latitude.Should().Be(24.7136m);
        location.Longitude.Should().Be(46.6753m);
    }

    [Fact]
    public async Task Handle_ShouldRejectLocationUpdates_WhenDriverIsBlocked()
    {
        await using var dbContext = CreateDbContext();
        var driverUser = new User("Blocked Geo Driver", "blocked.geo.driver@test.com", "01000000992", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567891", "DRV-GEO-2");
        driver.Approve(Guid.NewGuid());
        driver.BlockLocationUpdates(Guid.NewGuid(), "ops hold");

        dbContext.Users.Add(driverUser);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateDriverLocationCommandHandler(dbContext, dbContext);

        var act = () => handler.Handle(
            new UpdateDriverLocationCommand(driver.Id, 24.7136m, 46.6753m, 10m),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
        (await dbContext.DriverLocations.CountAsync()).Should().Be(0);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
