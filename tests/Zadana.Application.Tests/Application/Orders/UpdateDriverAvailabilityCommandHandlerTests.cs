using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Delivery.Commands.UpdateDriverAvailability;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Modules.Delivery.Repositories;
using Zadana.Infrastructure.Modules.Delivery.Services;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

public class UpdateDriverAvailabilityCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenDriverIsSoftBlocked_ShouldRejectAvailabilityEnable()
    {
        await using var dbContext = CreateDbContext();
        var driverUser = new User("Availability Driver", "availability.driver@test.com", "01000000177", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567890", "DRV-AVAIL-1");
        driver.Approve(Guid.NewGuid());
        dbContext.Users.Add(driverUser);
        dbContext.Drivers.Add(driver);

        for (var index = 0; index < 20; index++)
        {
            var attempt = new DeliveryOfferAttempt(Guid.NewGuid(), null, driver.Id, index + 1, DateTime.UtcNow.AddMinutes(1));
            attempt.MarkRejected("busy");
            dbContext.DeliveryOfferAttempts.Add(attempt);
        }

        await dbContext.SaveChangesAsync();

        var handler = new UpdateDriverAvailabilityCommandHandler(
            new DriverRepository(dbContext),
            new DriverCommitmentPolicyService(dbContext, dbContext),
            dbContext);

        var act = async () => await handler.Handle(
            new UpdateDriverAvailabilityCommand(driver.UserId, true),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.ErrorCode.Should().Be("DRIVER_SOFT_BLOCKED_BY_REJECTIONS");
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
