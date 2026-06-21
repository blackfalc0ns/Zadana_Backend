using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Social.Commands;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Social;

public sealed class CleanupStaleDriverOfferNotificationsCommandTests
{
    [Fact]
    public async Task Handle_WhenSyntheticOfferHasNoRealDriverAssignment_ShouldDeleteIt()
    {
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();

        context.Notifications.AddRange(
            new Notification(
                userId,
                "عرض توصيل تجريبي",
                "Test delivery offer",
                "عرض وهمي",
                "Synthetic offer",
                NotificationTypes.DriverDeliveryOffer,
                NotificationCategories.Dispatch,
                NotificationPriorities.Critical,
                Guid.NewGuid(),
                """{"event":"dispatch.offer_new","source":"admin_driver_test_offer_api"}"""),
            new Notification(
                userId,
                "انتهى العرض",
                "Offer expired",
                "انتهى العرض",
                "The offer expired",
                NotificationTypes.DriverDeliveryOffer,
                NotificationCategories.Dispatch,
                NotificationPriorities.Normal,
                Guid.NewGuid(),
                """{"event":"dispatch.offer_expired"}"""));
        await context.SaveChangesAsync();

        var handler = new CleanupStaleDriverOfferNotificationsCommandHandler(context);
        var deleted = await handler.Handle(
            new CleanupStaleDriverOfferNotificationsCommand(userId),
            CancellationToken.None);

        deleted.Should().Be(1);
        var remaining = await context.Notifications.AsNoTracking().ToListAsync();
        remaining.Should().ContainSingle();
        remaining[0].Data.Should().Contain("dispatch.offer_expired");
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
