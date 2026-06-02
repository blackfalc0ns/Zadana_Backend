using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Zadana.Api.Modules.Delivery.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Delivery;

public class AdminDriversControllerTests
{
    [Fact]
    public async Task SendDriverNotification_ShouldUseDriverUserIdAndPopupPayloadForPush()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var driverUser = new User(
            "Driver Test",
            "driver.test@zadana.com",
            "01000000177",
            UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "1234567890", "DRV-PUSH-1");

        dbContext.Users.Add(driverUser);
        dbContext.Drivers.Add(driver);
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
                ProviderNotificationId: "driver-push-id",
                Reason: null));

        var controller = CreateController(
            dbContext,
            notificationServiceMock.Object,
            oneSignalPushServiceMock.Object);

        const string titleAr = "\u0625\u0634\u0639\u0627\u0631 \u0645\u0646\u062F\u0648\u0628";
        const string titleEn = "Driver notification";
        const string bodyAr = "\u0627\u062E\u062A\u0628\u0627\u0631 \u0625\u0634\u0639\u0627\u0631 \u0627\u0644\u0645\u0646\u062F\u0648\u0628";
        const string bodyEn = "Driver notification test";
        var request = new AdminSendDriverNotificationRequest(
            TitleAr: titleAr,
            TitleEn: titleEn,
            BodyAr: bodyAr,
            BodyEn: bodyEn,
            TargetUrl: "/account-status");

        var result = await controller.SendDriverNotification(driver.Id, request, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<AdminDriverNotificationResponse>().Subject;
        response.DriverId.Should().Be(driver.Id);
        response.UserId.Should().Be(driverUser.Id);
        response.ExternalId.Should().Be(driverUser.Id.ToString());
        response.PushSent.Should().BeTrue();

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                driverUser.Id,
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                "driver_test",
                null,
                It.Is<string?>(data =>
                    data != null &&
                    data.Contains("admin_driver_notifications_test_api", StringComparison.Ordinal) &&
                    data.Contains("\"presentation\":\"popup\"", StringComparison.Ordinal) &&
                    data.Contains("\"popupType\":\"driver_account_updated\"", StringComparison.Ordinal) &&
                    data.Contains("\"showPopup\":true", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        oneSignalPushServiceMock.Verify(
            service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(pushRequest =>
                    pushRequest.ExternalUserId == driverUser.Id.ToString() &&
                    pushRequest.TitleAr == titleAr &&
                    pushRequest.TitleEn == titleEn &&
                    pushRequest.BodyAr == bodyAr &&
                    pushRequest.BodyEn == bodyEn &&
                    pushRequest.Type == "driver_test" &&
                    pushRequest.TargetUrl == "/account-status" &&
                    pushRequest.Category == "account" &&
                    pushRequest.TargetApplication == OneSignalApplicationTarget.Driver &&
                    pushRequest.Profile == OneSignalPushProfile.MobileHeadsUp &&
                    pushRequest.Data != null &&
                    pushRequest.Data.Contains("admin_driver_notifications_test_api", StringComparison.Ordinal) &&
                    pushRequest.Data.Contains("\"presentation\":\"popup\"", StringComparison.Ordinal) &&
                    pushRequest.Data.Contains("\"popupType\":\"driver_account_updated\"", StringComparison.Ordinal) &&
                    pushRequest.Data.Contains("\"showPopup\":true", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AdminDriversController CreateController(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        IOneSignalPushService oneSignalPushService) =>
        new(
            Mock.Of<IDriverReadService>(),
            dbContext,
            Mock.Of<IIdentityAccountService>(),
            notificationService,
            oneSignalPushService,
            NullLogger<AdminDriversController>.Instance);
}
