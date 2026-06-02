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
            .Setup(service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
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
            TitleAr = "\u0625\u0634\u0639\u0627\u0631 \u0623\u062f\u0645\u0646",
            TitleEn = "Admin notification",
            BodyAr = "\u0647\u0630\u0627 \u0627\u062e\u062a\u0628\u0627\u0631 \u0644\u0644\u0645\u0648\u0628\u0627\u064a\u0644",
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
                request.TitleAr,
                request.TitleEn,
                request.BodyAr,
                request.BodyEn,
                "customer_test",
                null,
                It.Is<string?>(data =>
                    data != null &&
                    data.Contains("admin_customer_notifications_test_api", StringComparison.Ordinal) &&
                    data.Contains("\"presentation\":\"popup\"", StringComparison.Ordinal) &&
                    data.Contains("\"popupType\":\"customer_test\"", StringComparison.Ordinal) &&
                    data.Contains("\"showPopup\":true", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        oneSignalPushServiceMock.Verify(
            service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(pushRequest =>
                    pushRequest.ExternalUserId == customer.Id.ToString() &&
                    pushRequest.TitleAr == request.TitleAr &&
                    pushRequest.TitleEn == request.TitleEn &&
                    pushRequest.BodyAr == request.BodyAr &&
                    pushRequest.BodyEn == request.BodyEn &&
                    pushRequest.Type == "customer_test" &&
                    pushRequest.TargetUrl == "/orders/test" &&
                    pushRequest.Profile == OneSignalPushProfile.MobileHeadsUp &&
                    pushRequest.Data != null &&
                    pushRequest.Data.Contains("admin_customer_notifications_test_api", StringComparison.Ordinal) &&
                    pushRequest.Data.Contains("\"presentation\":\"popup\"", StringComparison.Ordinal) &&
                    pushRequest.Data.Contains("\"popupType\":\"customer_test\"", StringComparison.Ordinal) &&
                    pushRequest.Data.Contains("\"showPopup\":true", StringComparison.Ordinal)),
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
            .Setup(service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
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
            service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(pushRequest =>
                    pushRequest.ExternalUserId == customer.Id.ToString() &&
                    pushRequest.TargetUrl == "/notifications" &&
                    pushRequest.Profile == OneSignalPushProfile.MobileHeadsUp &&
                    pushRequest.Data != null &&
                    pushRequest.Data.Contains("/notifications", StringComparison.Ordinal) &&
                    pushRequest.Data.Contains("\"showPopup\":true", StringComparison.Ordinal)),
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
