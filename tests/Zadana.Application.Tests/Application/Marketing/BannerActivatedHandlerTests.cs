using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.Events;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Marketing;

public class BannerActivatedHandlerTests
{
    [Fact]
    public async Task Handle_ShouldBroadcastAndPushEligibleCustomersOnly()
    {
        await using var dbContext = CreateDbContext();
        var eligibleCustomer = CreateUser("eligible-customer@test.com", UserRole.Customer, "01000000031");
        var disabledCustomer = CreateUser("disabled-customer@test.com", UserRole.Customer, "01000000032");
        var inactiveCustomer = CreateUser("inactive-customer@test.com", UserRole.Customer, "01000000033");
        var vendorUser = CreateUser("vendor-user@test.com", UserRole.Vendor, "01000000034");

        var eligiblePrimaryDevice = CreateDevice(eligibleCustomer.Id, "token-1", "device-1", notificationsEnabled: true);
        var eligibleSecondaryDevice = CreateDevice(eligibleCustomer.Id, "token-2", "device-2", notificationsEnabled: true);
        var disabledDevice = CreateDevice(disabledCustomer.Id, "token-3", "device-3", notificationsEnabled: false);
        var inactiveDevice = CreateDevice(inactiveCustomer.Id, "token-4", "device-4", notificationsEnabled: true);
        inactiveDevice.Deactivate();
        var vendorDevice = CreateDevice(vendorUser.Id, "token-5", "device-5", notificationsEnabled: true);

        dbContext.Users.AddRange(eligibleCustomer, disabledCustomer, inactiveCustomer, vendorUser);
        dbContext.UserPushDevices.AddRange(
            eligiblePrimaryDevice,
            eligibleSecondaryDevice,
            disabledDevice,
            inactiveDevice,
            vendorDevice);
        await dbContext.SaveChangesAsync();

        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = new Mock<IOneSignalPushService>();
        pushServiceMock
            .Setup(service => service.SendToExternalUsersAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<OneSignalPushProfile>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new OneSignalPushDispatchResult(
                    Attempted: true,
                    Sent: true,
                    Skipped: false,
                    ProviderStatusCode: 200,
                    ProviderNotificationId: "push-id",
                    Reason: null)
            ]);

        var handler = new BannerActivatedHandler(notificationServiceMock.Object, dbContext, pushServiceMock.Object);
        var notification = new BannerActivatedNotification(
            Guid.NewGuid(),
            "عرض رمضان",
            "Ramadan Offer",
            "https://cdn.test/banner.png");

        await handler.Handle(notification, CancellationToken.None);

        notificationServiceMock.Verify(
            service => service.BroadcastToAllCustomersAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.NewBanner,
                It.Is<string?>(data =>
                    data != null &&
                    data.Contains(notification.BannerId.ToString()) &&
                    data.Contains(notification.ImageUrl)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        pushServiceMock.Verify(
            service => service.SendToExternalUsersAsync(
                It.Is<IReadOnlyCollection<string>>(ids =>
                    ids.Count == 1 &&
                    ids.Single() == eligibleCustomer.Id.ToString()),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.NewBanner,
                null,
                It.Is<string?>(data =>
                    data != null &&
                    data.Contains(notification.BannerId.ToString()) &&
                    data.Contains(notification.ImageUrl)),
                null,
                OneSignalPushProfile.MobileHeadsUp,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoEligibleCustomers_ShouldStillBroadcastWithoutPush()
    {
        await using var dbContext = CreateDbContext();
        var vendorUser = CreateUser("vendor-only@test.com", UserRole.Vendor, "01000000035");
        var vendorDevice = CreateDevice(vendorUser.Id, "token-9", "device-9", notificationsEnabled: true);

        dbContext.Users.Add(vendorUser);
        dbContext.UserPushDevices.Add(vendorDevice);
        await dbContext.SaveChangesAsync();

        var notificationServiceMock = new Mock<INotificationService>();
        var pushServiceMock = new Mock<IOneSignalPushService>();
        var handler = new BannerActivatedHandler(notificationServiceMock.Object, dbContext, pushServiceMock.Object);

        await handler.Handle(
            new BannerActivatedNotification(
                Guid.NewGuid(),
                "عرض جديد",
                "New Offer",
                "https://cdn.test/banner-2.png"),
            CancellationToken.None);

        notificationServiceMock.Verify(
            service => service.BroadcastToAllCustomersAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.NewBanner,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        pushServiceMock.Verify(
            service => service.SendToExternalUsersAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<OneSignalPushProfile>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static User CreateUser(string email, UserRole role, string phone) =>
        new("Test User", email, phone, role);

    private static UserPushDevice CreateDevice(Guid userId, string token, string deviceId, bool notificationsEnabled) =>
        new(
            userId,
            token,
            PushPlatform.Fcm,
            deviceId,
            "Pixel",
            "1.0.0",
            "ar",
            notificationsEnabled);
}
