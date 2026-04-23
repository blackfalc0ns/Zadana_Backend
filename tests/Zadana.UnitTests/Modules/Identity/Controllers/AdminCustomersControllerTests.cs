using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Zadana.Api.Modules.Identity.Controllers;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Identity.Controllers;

public class AdminCustomersControllerTests
{
    [Fact]
    public async Task SendCustomerNotification_ShouldQueueInboxAndMobilePush()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var customer = new User(
            "Customer Test",
            "customer.test@zadana.com",
            "01000000088",
            UserRole.Customer);
        dbContext.Users.Add(customer);
        await dbContext.SaveChangesAsync();

        var notificationServiceMock = new Mock<INotificationService>();
        var oneSignalPushServiceMock = new Mock<IOneSignalPushService>();
        oneSignalPushServiceMock
            .Setup(service => service.SendToExternalUserAsync(
                It.IsAny<string>(),
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
            .ReturnsAsync(new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: true,
                Skipped: false,
                ProviderStatusCode: 200,
                ProviderNotificationId: "push-id",
                Reason: null));

        var controller = CreateController(dbContext, notificationServiceMock.Object, oneSignalPushServiceMock.Object);
        var request = new AdminSendCustomerNotificationRequest
        {
            TitleAr = "إشعار أدمن",
            TitleEn = "Admin notification",
            BodyAr = "هذا اختبار للموبايل",
            BodyEn = "This is a mobile test",
            Type = "customer_test",
            TargetUrl = "/orders/test"
        };

        var result = await controller.SendCustomerNotification(customer.Id, request, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<AdminCustomerNotificationResponse>().Subject;
        response.CustomerId.Should().Be(customer.Id);
        response.UserId.Should().Be(customer.Id);
        response.ExternalId.Should().Be(customer.Id.ToString());
        response.PushSent.Should().BeTrue();

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                customer.Id,
                "إشعار أدمن",
                "Admin notification",
                "هذا اختبار للموبايل",
                "This is a mobile test",
                "customer_test",
                null,
                It.Is<string?>(data => data != null && data.Contains("admin_customer_notifications_test_api")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        oneSignalPushServiceMock.Verify(
            service => service.SendToExternalUserAsync(
                customer.Id.ToString(),
                "إشعار أدمن",
                "Admin notification",
                "هذا اختبار للموبايل",
                "This is a mobile test",
                "customer_test",
                null,
                It.Is<string?>(data => data != null && data.Contains("admin_customer_notifications_test_api")),
                "/orders/test",
                OneSignalPushProfile.MobileHeadsUp,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendCustomerNotification_ShouldDefaultTargetUrlToNotifications()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var customer = new User(
            "Customer Test",
            "customer.default@zadana.com",
            "01000000089",
            UserRole.Customer);
        dbContext.Users.Add(customer);
        await dbContext.SaveChangesAsync();

        var notificationServiceMock = new Mock<INotificationService>();
        var oneSignalPushServiceMock = new Mock<IOneSignalPushService>();
        oneSignalPushServiceMock
            .Setup(service => service.SendToExternalUserAsync(
                It.IsAny<string>(),
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
            .ReturnsAsync(new OneSignalPushDispatchResult(
                Attempted: true,
                Sent: true,
                Skipped: false,
                ProviderStatusCode: 200,
                ProviderNotificationId: "push-id",
                Reason: null));

        var controller = CreateController(dbContext, notificationServiceMock.Object, oneSignalPushServiceMock.Object);

        await controller.SendCustomerNotification(customer.Id, new AdminSendCustomerNotificationRequest(), CancellationToken.None);

        oneSignalPushServiceMock.Verify(
            service => service.SendToExternalUserAsync(
                customer.Id.ToString(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.Is<string?>(data => data != null && data.Contains("/notifications")),
                "/notifications",
                OneSignalPushProfile.MobileHeadsUp,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AdminCustomersController CreateController(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService) =>
        new(
            dbContext,
            notificationService,
            oneSignalPushService,
            NullLogger<AdminCustomersController>.Instance);
}
