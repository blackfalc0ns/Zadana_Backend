using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Commands.ApproveDriverDocumentReview;
using Zadana.Application.Modules.Delivery.Commands.RejectDriverDocumentReview;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Social.Enums;

namespace Zadana.Application.Tests.Application.Orders;

public class DriverDocumentReviewNotificationCommandTests
{
    private readonly Mock<IDriverRepository> _driverRepositoryMock = new();
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IIdentityAccountService> _identityAccountServiceMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<IOneSignalPushService> _oneSignalPushServiceMock = new();

    [Fact]
    public async Task ApproveDocument_ShouldSendDriverNotificationAndPush()
    {
        var reviewerId = Guid.NewGuid();
        var driver = CreateDriver();
        ConfigureActor(reviewerId);
        _driverRepositoryMock
            .Setup(repository => repository.GetByIdWithReviewsAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);
        _dbContextMock
            .Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new ApproveDriverDocumentReviewCommandHandler(
            _driverRepositoryMock.Object,
            _dbContextMock.Object,
            _currentUserServiceMock.Object,
            _identityAccountServiceMock.Object,
            _notificationServiceMock.Object,
            _oneSignalPushServiceMock.Object);

        await handler.Handle(
            new ApproveDriverDocumentReviewCommand(driver.Id, DriverDocumentType.DriverLicense.ToString()),
            CancellationToken.None);

        _notificationServiceMock.Verify(service => service.SendToUserAsync(
                driver.UserId,
                It.Is<NotificationDispatchRequest>(request =>
                    request.Type == NotificationTypes.DriverAccountUpdated &&
                    request.Category == NotificationCategories.Account &&
                    request.TitleEn.Contains("Driver license approved") &&
                    request.Data != null &&
                    request.Data.Contains("documentType") &&
                    request.Data.Contains("DriverLicense")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _notificationServiceMock.Verify(service => service.SendDriverHomeUpdatedAsync(
                driver.UserId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _oneSignalPushServiceMock.Verify(service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(request =>
                    request.ExternalUserId == driver.UserId.ToString() &&
                    request.Type == NotificationTypes.DriverAccountUpdated &&
                    request.TargetApplication == OneSignalApplicationTarget.Driver &&
                    request.TargetUrl == "/account-status"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RejectDocument_ShouldSendDriverCorrectionNotificationAndPush()
    {
        var reviewerId = Guid.NewGuid();
        var driver = CreateDriver();
        ConfigureActor(reviewerId);
        _driverRepositoryMock
            .Setup(repository => repository.GetByIdWithReviewsAsync(driver.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);
        _dbContextMock
            .Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new RejectDriverDocumentReviewCommandHandler(
            _driverRepositoryMock.Object,
            _dbContextMock.Object,
            _currentUserServiceMock.Object,
            _identityAccountServiceMock.Object,
            _notificationServiceMock.Object,
            _oneSignalPushServiceMock.Object);

        await handler.Handle(
            new RejectDriverDocumentReviewCommand(
                driver.Id,
                DriverDocumentType.VehicleLicense.ToString(),
                "Expiry date image is unclear"),
            CancellationToken.None);

        _notificationServiceMock.Verify(service => service.SendToUserAsync(
                driver.UserId,
                It.Is<NotificationDispatchRequest>(request =>
                    request.Type == NotificationTypes.DriverAccountUpdated &&
                    request.Priority == NotificationPriorities.Critical &&
                    request.TitleEn.Contains("Vehicle license needs correction") &&
                    request.BodyEn.Contains("Expiry date image is unclear") &&
                    request.Data != null &&
                    request.Data.Contains("VehicleLicense")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _notificationServiceMock.Verify(service => service.SendDriverHomeUpdatedAsync(
                driver.UserId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _oneSignalPushServiceMock.Verify(service => service.SendMobileNotificationAsync(
                It.Is<OneSignalMobilePushRequest>(request =>
                    request.ExternalUserId == driver.UserId.ToString() &&
                    request.Profile == OneSignalPushProfile.MobileHeadsUp &&
                    request.Data != null &&
                    request.Data.Contains("Expiry date image is unclear")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        driver.VerificationStatus.Should().Be(DriverVerificationStatus.NeedsDocuments);
        driver.DocumentReviews.Should().ContainSingle(item =>
            item.Type == DriverDocumentType.VehicleLicense &&
            item.Decision == DriverDocumentReviewDecision.Rejected &&
            item.RejectionReason == "Expiry date image is unclear");
    }

    private void ConfigureActor(Guid reviewerId)
    {
        _currentUserServiceMock.SetupGet(service => service.UserId).Returns(reviewerId);
        _identityAccountServiceMock
            .Setup(service => service.FindByIdAsync(reviewerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityAccountSnapshot(
                reviewerId,
                "Compliance Reviewer",
                "reviewer@test.com",
                "01000000001",
                UserRole.Admin,
                1,
                AccountStatus.Active,
                false,
                null,
                null,
                true,
                true));

        _oneSignalPushServiceMock
            .Setup(service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(true, true, false, 200, "mock-id", null));
    }

    private static Driver CreateDriver()
    {
        var user = new User("Driver Notifications", "driver.notifications@test.com", "01000000999", UserRole.Driver);
        var driver = new Driver(
            user.Id,
            DriverVehicleType.Car,
            "12345678901234",
            "DL-12345",
            nationalIdExpiryDate: DateTime.UtcNow.Date.AddYears(2),
            driverLicenseExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            vehicleLicenseNumber: "VL-7890",
            vehicleLicenseExpiryDate: DateTime.UtcNow.Date.AddYears(1),
            address: "Cairo",
            nationalIdFrontImageUrl: "https://cdn/id-front.png",
            nationalIdBackImageUrl: "https://cdn/id-back.png",
            licenseImageUrl: "https://cdn/license.png",
            vehicleImageUrl: "https://cdn/vehicle-license.png",
            personalPhotoUrl: "https://cdn/photo.png");

        typeof(Driver).GetProperty(nameof(Driver.User))!.SetValue(driver, user);
        return driver;
    }
}
