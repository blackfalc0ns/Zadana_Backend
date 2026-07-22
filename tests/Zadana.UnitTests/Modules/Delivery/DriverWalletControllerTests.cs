using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Zadana.Api.Modules.Delivery.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Api.Modules.Wallets.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Settings;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Wallets.DTOs;
using Zadana.Application.Modules.Wallets.Services;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Serialization;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Delivery;

public class DriverWalletControllerTests
{
    [Fact]
    public async Task CreatePaymentMethod_ThrowsBadRequestException_WhenRequestBodyIsMissing()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);
        var profileChangeApprovalService = Mock.Of<IProfileChangeApprovalService>();

        var act = () => controller.CreatePaymentMethod(
            null,
            currentUserService,
            driverRepository.Object,
            profileChangeApprovalService,
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Request body is required.");
    }

    [Fact]
    public async Task CreatePaymentMethod_ThrowsBadRequestException_WhenAccountIdentifierIsBlank()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);
        var profileChangeApprovalService = Mock.Of<IProfileChangeApprovalService>();

        var act = () => controller.CreatePaymentMethod(
            new CreateDriverPayoutMethodRequest("BankAccount", "Driver Name", " ", "Bank", true),
            currentUserService,
            driverRepository.Object,
            profileChangeApprovalService,
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Account identifier is required.");
    }

    [Fact]
    public async Task CreatePaymentMethod_SubmitsApprovalRequest_WithoutCreatingMethod()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var approvalRequestId = Guid.NewGuid();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);
        var profileChangeApprovalService = new Mock<IProfileChangeApprovalService>();
        profileChangeApprovalService
            .Setup(service => service.SubmitAsync(
                driver.UserId,
                driver.UserId,
                ProfileChangeApprovalActions.DriverPayoutMethodCreate,
                It.IsAny<string>(),
                It.IsAny<DriverPayoutMethodCreatePayload>(),
                It.IsAny<ProfileChangeApprovalAlert>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(approvalRequestId);

        var result = await controller.CreatePaymentMethod(
            new CreateDriverPayoutMethodRequest("BankAccount", "Driver Name", "SA1234567890123456789012", "Bank", true),
            currentUserService,
            driverRepository.Object,
            profileChangeApprovalService.Object,
            CancellationToken.None);

        result.Result.Should().BeOfType<AcceptedResult>();
        context.DriverPayoutMethods.Should().BeEmpty();
        profileChangeApprovalService.Verify(
            service => service.SubmitAsync(
                driver.UserId,
                driver.UserId,
                ProfileChangeApprovalActions.DriverPayoutMethodCreate,
                It.IsAny<string>(),
                It.IsAny<DriverPayoutMethodCreatePayload>(),
                It.IsAny<ProfileChangeApprovalAlert>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeletePaymentMethod_ThrowsBusinessRuleException_WhenMethodHasWithdrawalHistory()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);
        var profileChangeApprovalService = Mock.Of<IProfileChangeApprovalService>();

        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        var payoutMethod = new DriverPayoutMethod(driver.Id, DriverPayoutMethodType.BankAccount, "Driver Name", "1234567890", "Bank", true);
        var withdrawal = new DriverWithdrawalRequest(driver.Id, wallet.Id, payoutMethod.Id, 25m);

        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        context.DriverWithdrawalRequests.Add(withdrawal);
        await context.SaveChangesAsync(CancellationToken.None);

        var act = () => controller.DeletePaymentMethod(
            payoutMethod.Id,
            currentUserService,
            driverRepository.Object,
            context,
            profileChangeApprovalService,
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*linked to withdrawal requests*");
    }

    [Fact]
    public async Task DeletePaymentMethod_SubmitsApprovalRequest_WithoutDeletingMethod()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var approvalRequestId = Guid.NewGuid();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);
        var payoutMethod = new DriverPayoutMethod(driver.Id, DriverPayoutMethodType.BankAccount, "Driver Name", "SA1234567890123456789012", "Bank", true);
        var profileChangeApprovalService = new Mock<IProfileChangeApprovalService>();
        profileChangeApprovalService
            .Setup(service => service.SubmitAsync(
                driver.UserId,
                driver.UserId,
                ProfileChangeApprovalActions.DriverPayoutMethodDelete,
                It.IsAny<string>(),
                It.IsAny<DriverPayoutMethodDeletePayload>(),
                It.IsAny<ProfileChangeApprovalAlert>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(approvalRequestId);

        context.DriverPayoutMethods.Add(payoutMethod);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await controller.DeletePaymentMethod(
            payoutMethod.Id,
            currentUserService,
            driverRepository.Object,
            context,
            profileChangeApprovalService.Object,
            CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
        context.DriverPayoutMethods.Should().ContainSingle(item => item.Id == payoutMethod.Id);
        profileChangeApprovalService.Verify(
            service => service.SubmitAsync(
                driver.UserId,
                driver.UserId,
                ProfileChangeApprovalActions.DriverPayoutMethodDelete,
                It.IsAny<string>(),
                It.IsAny<DriverPayoutMethodDeletePayload>(),
                It.IsAny<ProfileChangeApprovalAlert>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateWithdrawal_ThrowsBusinessRuleException_WhenSpecifiedPaymentMethodDoesNotBelongToDriver()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var notificationService = Mock.Of<INotificationService>();
        var driver = CreateApprovedDriver();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);

        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        wallet.Credit(100m);
        context.Wallets.Add(wallet);
        await context.SaveChangesAsync(CancellationToken.None);

        var adminAlertService = Mock.Of<IAdminAlertService>();
        var driverWalletNotificationService = Mock.Of<IDriverWalletNotificationService>();
        var act = () => controller.CreateWithdrawal(
            new CreateDriverWithdrawalRequest(Guid.NewGuid(), 20m),
            currentUserService,
            driverRepository.Object,
            context,
            driverWalletNotificationService,
            adminAlertService,
            NullLogger<DriverWalletController>.Instance,
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*selected payout method was not found*");
    }

    [Fact]
    public async Task CreateWithdrawal_WithSameIdempotencyKey_ReturnsSameRequestAndSingleHold()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);
        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        wallet.Credit(100m);
        var payoutMethod = new DriverPayoutMethod(
            driver.Id,
            DriverPayoutMethodType.BankAccount,
            "Driver Name",
            "SA1234567890123456789012",
            "Bank",
            true);
        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        await context.SaveChangesAsync();

        var notificationService = CreateDriverWalletNotificationService();
        var adminAlertService = CreateAdminAlertService();
        var request = new CreateDriverWithdrawalRequest(payoutMethod.Id, 40m, "mobile-request-001");

        var first = await controller.CreateWithdrawal(
            request,
            currentUserService,
            driverRepository.Object,
            context,
            notificationService.Object,
            adminAlertService.Object,
            NullLogger<DriverWalletController>.Instance);
        var second = await controller.CreateWithdrawal(
            request,
            currentUserService,
            driverRepository.Object,
            context,
            notificationService.Object,
            adminAlertService.Object,
            NullLogger<DriverWalletController>.Instance);

        var firstDto = ((OkObjectResult)first.Result!).Value.Should()
            .BeOfType<DriverWithdrawalRequestDto>().Subject;
        var secondDto = ((OkObjectResult)second.Result!).Value.Should()
            .BeOfType<DriverWithdrawalRequestDto>().Subject;
        secondDto.Id.Should().Be(firstDto.Id);
        context.DriverWithdrawalRequests.Should().ContainSingle(item =>
            item.Id == firstDto.Id &&
            item.RequestIdempotencyKey == "mobile-request-001" &&
            item.RequestedPayoutDay == driver.PayoutDay &&
            item.DestinationSnapshot != null);
        context.WalletHolds.Should().ContainSingle(item =>
            item.ReferenceId == firstDto.Id && item.Status == WalletHoldStatus.Active);

        var mismatchedRetry = () => controller.CreateWithdrawal(
            new CreateDriverWithdrawalRequest(payoutMethod.Id, 41m, "mobile-request-001"),
            currentUserService,
            driverRepository.Object,
            context,
            notificationService.Object,
            adminAlertService.Object,
            NullLogger<DriverWalletController>.Instance);
        await mismatchedRetry.Should().ThrowAsync<BusinessRuleException>()
            .Where(error => error.ErrorCode == "WITHDRAWAL_IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task CreateWithdrawal_WithDifferentKey_RejectsSecondActiveRequest()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);
        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        wallet.Credit(100m);
        var payoutMethod = new DriverPayoutMethod(
            driver.Id,
            DriverPayoutMethodType.BankAccount,
            "Driver Name",
            "SA1234567890123456789012",
            "Bank",
            true);
        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        await context.SaveChangesAsync();

        var notificationService = CreateDriverWalletNotificationService();
        var adminAlertService = CreateAdminAlertService();
        await controller.CreateWithdrawal(
            new CreateDriverWithdrawalRequest(payoutMethod.Id, 40m, "mobile-request-001"),
            currentUserService,
            driverRepository.Object,
            context,
            notificationService.Object,
            adminAlertService.Object,
            NullLogger<DriverWalletController>.Instance);

        var act = () => controller.CreateWithdrawal(
            new CreateDriverWithdrawalRequest(payoutMethod.Id, 30m, "mobile-request-002"),
            currentUserService,
            driverRepository.Object,
            context,
            notificationService.Object,
            adminAlertService.Object,
            NullLogger<DriverWalletController>.Instance);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(error => error.ErrorCode == "DRIVER_ACTIVE_WITHDRAWAL_EXISTS");
        context.DriverWithdrawalRequests.Should().HaveCount(1);
        context.WalletHolds.Should().HaveCount(1);
    }

    [Fact]
    public async Task CancelWithdrawal_CancelsPendingRequestAndReleasesItsHold()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);
        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        wallet.Credit(100m);
        var payoutMethod = new DriverPayoutMethod(
            driver.Id,
            DriverPayoutMethodType.BankAccount,
            "Driver Name",
            "SA1234567890123456789012",
            "Bank",
            true);
        var withdrawal = new DriverWithdrawalRequest(
            driver.Id,
            wallet.Id,
            payoutMethod.Id,
            40m,
            "mobile-request-cancel",
            driver.PayoutDay,
            PayoutDestinationSnapshotCodec.CreateDriverPayoutMethod(payoutMethod));
        var hold = new WalletHold(
            WalletOwnerType.Driver,
            driver.Id,
            withdrawal.Amount,
            WalletHoldReason.Withdrawal,
            $"driver-withdrawal:{withdrawal.Id:N}",
            walletId: wallet.Id,
            referenceType: "DriverWithdrawalRequest",
            referenceId: withdrawal.Id,
            memo: "test withdrawal");
        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        context.DriverWithdrawalRequests.Add(withdrawal);
        context.WalletHolds.Add(hold);
        await context.SaveChangesAsync();

        var driverWalletNotificationService = CreateDriverWalletNotificationService();
        var result = await controller.CancelWithdrawal(
            withdrawal.Id,
            currentUserService,
            driverRepository.Object,
            context,
            driverWalletNotificationService.Object);

        ((OkObjectResult)result.Result!).Value.Should()
            .BeOfType<DriverWithdrawalRequestDto>()
            .Which.Status.Should().Be(DriverWithdrawalStatus.Cancelled.ToString());
        withdrawal.Status.Should().Be(DriverWithdrawalStatus.Cancelled);
        hold.Status.Should().Be(WalletHoldStatus.Cancelled);
        driverWalletNotificationService.Verify(
            service => service.NotifyWithdrawalCancelledAsync(
                driver.UserId,
                It.Is<DriverWithdrawalRequest>(item => item.Id == withdrawal.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetWithdrawalSettings_ReturnsServerLimitsUsageAndActiveState()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        driver.UpdatePayoutDay(PayoutScheduleDay.Thursday);
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);
        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        var payoutMethod = new DriverPayoutMethod(
            driver.Id,
            DriverPayoutMethodType.BankAccount,
            "Driver Name",
            "SA1234567890123456789012",
            "Bank",
            true);
        context.DriverWithdrawalRequests.Add(new DriverWithdrawalRequest(
            driver.Id,
            wallet.Id,
            payoutMethod.Id,
            40m));
        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        await context.SaveChangesAsync();
        var settings = new SettlementProcessingSettingsService(context);
        var limits = Options.Create(new FinancialSettingsOptions
        {
            DriverMinimumWithdrawalAmount = 25m,
            DriverMaximumWithdrawalAmount = 1_000m,
            DriverMaximumWithdrawalRequestsPerDay = 2
        });

        var result = await controller.GetWithdrawalSettings(
            currentUserService,
            driverRepository.Object,
            context,
            settings,
            limits);

        ((OkObjectResult)result.Result!).Value.Should()
            .BeEquivalentTo(new DriverWithdrawalSettingsDto(
                25m,
                1_000m,
                2,
                1,
                true,
                "SAR",
                "Thursday",
                new[] { "Monday", "Thursday" }));
    }

    [Fact]
    public async Task UpdatePayoutPreference_StoresSelectedThursday()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);
        var settlementSettings = new SettlementProcessingSettingsService(context);

        var result = await controller.UpdatePayoutPreference(
            new UpdateDriverPayoutPreferenceRequest("thursday"),
            currentUserService,
            driverRepository.Object,
            context,
            settlementSettings,
            CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(
                new DriverPayoutPreferenceDto("Thursday", ["Monday", "Thursday"]));
        driver.PayoutDay.Should().Be(PayoutScheduleDay.Thursday);
    }

    [Fact]
    public async Task UpdatePayoutPreference_RejectsDisabledDay()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);
        var settlementSettings = new SettlementProcessingSettingsService(context);

        var act = () => controller.UpdatePayoutPreference(
            new UpdateDriverPayoutPreferenceRequest("Sunday"),
            currentUserService,
            driverRepository.Object,
            context,
            settlementSettings,
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*not enabled by the platform*");
    }

    private static Mock<IDriverRepository> CreateDriverRepository(Driver driver)
    {
        var repository = new Mock<IDriverRepository>();
        repository
            .Setup(item => item.GetByUserIdAsync(driver.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);
        return repository;
    }

    private static Driver CreateApprovedDriver()
    {
        var driver = new Driver(
            Guid.NewGuid(),
            null,
            null,
            null,
            region: "RIYADH",
            city: "RIYADH");
        driver.Approve(Guid.NewGuid(), "approved for wallet tests");
        return driver;
    }

    private static Mock<IDriverWalletNotificationService> CreateDriverWalletNotificationService()
    {
        var service = new Mock<IDriverWalletNotificationService>();
        service.Setup(item => item.NotifyWithdrawalSubmittedAsync(
                It.IsAny<Guid>(),
                It.IsAny<DriverWithdrawalRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        service.Setup(item => item.NotifyWithdrawalCancelledAsync(
                It.IsAny<Guid>(),
                It.IsAny<DriverWithdrawalRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        service.Setup(item => item.NotifyWithdrawalProcessingAsync(
                It.IsAny<Guid>(),
                It.IsAny<DriverWithdrawalRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        service.Setup(item => item.NotifyWithdrawalRejectedAsync(
                It.IsAny<Guid>(),
                It.IsAny<DriverWithdrawalRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return service;
    }

    private static Mock<INotificationService> CreateNotificationService()
    {
        var service = new Mock<INotificationService>();
        service.Setup(item => item.SendToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<NotificationDispatchRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        service.Setup(item => item.SendDriverWalletUpdatedAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return service;
    }

    private static Mock<IOneSignalPushService> CreatePushService()
    {
        var service = new Mock<IOneSignalPushService>();
        service.Setup(item => item.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(false, false, true, null, null, "test"));
        return service;
    }

    private static Mock<IAdminAlertService> CreateAdminAlertService()
    {
        var service = new Mock<IAdminAlertService>();
        service.Setup(item => item.SendAsync(
                It.IsAny<AdminAlertRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAlertDispatchResult(
                0,
                0,
                new OneSignalPushDispatchResult(false, false, true, null, null, "test")));
        return service;
    }
}

public class AdminWalletsControllerTests
{
    [Fact]
    public async Task ProcessWithdrawal_InManualMode_PreparesLinkedPayoutAndReturnsConfirmationMetadata()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new AdminWalletsController();
        var driver = CreateApprovedDriver();
        driver.UpdatePayoutDay((PayoutScheduleDay)SaudiTime.Today.DayOfWeek);
        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        wallet.Credit(100m);
        var payoutMethod = new DriverPayoutMethod(
            driver.Id,
            DriverPayoutMethodType.BankAccount,
            "Driver Name",
            "SA1234567890123456789012",
            "Bank",
            true);
        var withdrawal = new DriverWithdrawalRequest(driver.Id, wallet.Id, payoutMethod.Id, 40m);
        var settlementSettings = new Mock<ISettlementProcessingSettingsService>();
        settlementSettings
            .Setup(service => service.IsAutomaticAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        settlementSettings
            .Setup(service => service.GetEnabledPayoutDaysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([(PayoutScheduleDay)SaudiTime.Today.DayOfWeek]);

        context.Drivers.Add(driver);
        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        context.DriverWithdrawalRequests.Add(withdrawal);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await controller.ProcessWithdrawal(
            withdrawal.Id,
            new AdminProcessWithdrawalRequest(true, null, null),
            context,
            null!,
            null!,
            null!,
            Mock.Of<IDriverWalletNotificationService>(),
            CancellationToken.None,
            currentUserService: CreateAdminCurrentUser(),
            payoutOrchestrator: null,
            moyasarSettings: null,
            settlementProcessingSettingsService: settlementSettings.Object);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AdminProcessWithdrawalResultDto>().Subject;

        withdrawal.Status.Should().Be(DriverWithdrawalStatus.Processing);
        withdrawal.PayoutId.Should().NotBeNull();
        withdrawal.ReviewedByUserId.Should().NotBeNull();
        withdrawal.ReviewedAtUtc.Should().NotBeNull();
        response.PayoutId.Should().Be(withdrawal.PayoutId);
        response.PayoutStatus.Should().Be(PayoutStatus.Pending.ToString());
        response.ManualWorkflowRequired.Should().BeTrue();
        response.ManualClaimEndpoint.Should().Be($"/api/admin/payouts/{withdrawal.PayoutId}/manual-claim");
        response.ManualBankSubmissionEndpoint.Should().Be($"/api/admin/payouts/{withdrawal.PayoutId}/manual-bank-submission");
        response.ManualConfirmationEndpoint.Should().Be($"/api/admin/payouts/{withdrawal.PayoutId}/confirm-manual");
        response.TransferReference.Should().BeNull();
        context.Settlements.Should().ContainSingle(item =>
            item.DriverId == driver.Id &&
            item.Status == SettlementStatus.Approved &&
            item.NetAmount == withdrawal.Amount);
        context.Payouts.Should().ContainSingle(item =>
            item.Id == withdrawal.PayoutId &&
            item.Status == PayoutStatus.Pending &&
            item.Settlement.Status == SettlementStatus.Approved);
        context.WalletHolds.Should().ContainSingle(item =>
            item.ReferenceId == withdrawal.Id &&
            item.Status == WalletHoldStatus.Active &&
            item.Amount == withdrawal.Amount);
    }

    [Fact]
    public async Task ProcessWithdrawal_ManualPreparation_IsIdempotent_WhenPayoutIsAlreadyLinked()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new AdminWalletsController();
        var driver = CreateApprovedDriver();
        driver.UpdatePayoutDay((PayoutScheduleDay)SaudiTime.Today.DayOfWeek);
        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        wallet.Credit(100m);
        var payoutMethod = new DriverPayoutMethod(
            driver.Id,
            DriverPayoutMethodType.BankAccount,
            "Driver Name",
            "SA1234567890123456789012",
            "Bank",
            true);
        var withdrawal = new DriverWithdrawalRequest(driver.Id, wallet.Id, payoutMethod.Id, 40m);
        var settlementSettings = new Mock<ISettlementProcessingSettingsService>();
        settlementSettings
            .Setup(service => service.IsAutomaticAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        settlementSettings
            .Setup(service => service.GetEnabledPayoutDaysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([(PayoutScheduleDay)SaudiTime.Today.DayOfWeek]);

        context.Drivers.Add(driver);
        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        context.DriverWithdrawalRequests.Add(withdrawal);
        await context.SaveChangesAsync(CancellationToken.None);

        var first = await controller.ProcessWithdrawal(
            withdrawal.Id,
            new AdminProcessWithdrawalRequest(true, null, null),
            context,
            null!,
            null!,
            null!,
            Mock.Of<IDriverWalletNotificationService>(),
            CancellationToken.None,
            currentUserService: CreateAdminCurrentUser(),
            payoutOrchestrator: null,
            moyasarSettings: null,
            settlementProcessingSettingsService: settlementSettings.Object);
        var second = await controller.ProcessWithdrawal(
            withdrawal.Id,
            new AdminProcessWithdrawalRequest(true, "ignored-manual-reference", null),
            context,
            null!,
            null!,
            null!,
            Mock.Of<IDriverWalletNotificationService>(),
            CancellationToken.None,
            currentUserService: CreateAdminCurrentUser(),
            payoutOrchestrator: null,
            moyasarSettings: null,
            settlementProcessingSettingsService: settlementSettings.Object);

        var firstResponse = ((OkObjectResult)first.Result!).Value.Should()
            .BeOfType<AdminProcessWithdrawalResultDto>().Subject;
        var secondResponse = ((OkObjectResult)second.Result!).Value.Should()
            .BeOfType<AdminProcessWithdrawalResultDto>().Subject;

        firstResponse.PayoutId.Should().NotBeNull();
        secondResponse.PayoutId.Should().Be(firstResponse.PayoutId);
        context.Settlements.Should().HaveCount(1);
        context.Payouts.Should().HaveCount(1);
        context.WalletHolds.Should().ContainSingle(item =>
            item.ReferenceId == withdrawal.Id && item.Status == WalletHoldStatus.Active);
        withdrawal.TransferReference.Should().BeNull("manual confirmation owns the bank reference");
    }

    [Fact]
    public async Task ProcessWithdrawal_RejectPreparedManualPayout_CancelsTheLinkedPayoutAndHold()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new AdminWalletsController();
        var driver = CreateApprovedDriver();
        driver.UpdatePayoutDay((PayoutScheduleDay)SaudiTime.Today.DayOfWeek);
        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        wallet.Credit(100m);
        var payoutMethod = new DriverPayoutMethod(
            driver.Id,
            DriverPayoutMethodType.BankAccount,
            "Driver Name",
            "SA1234567890123456789012",
            "Bank",
            true);
        var withdrawal = new DriverWithdrawalRequest(driver.Id, wallet.Id, payoutMethod.Id, 40m);
        var settlementSettings = new Mock<ISettlementProcessingSettingsService>();
        settlementSettings
            .Setup(service => service.IsAutomaticAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        settlementSettings
            .Setup(service => service.GetEnabledPayoutDaysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([(PayoutScheduleDay)SaudiTime.Today.DayOfWeek]);

        context.Drivers.Add(driver);
        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        context.DriverWithdrawalRequests.Add(withdrawal);
        await context.SaveChangesAsync(CancellationToken.None);

        await controller.ProcessWithdrawal(
            withdrawal.Id,
            new AdminProcessWithdrawalRequest(true, null, null),
            context,
            null!,
            null!,
            null!,
            Mock.Of<IDriverWalletNotificationService>(),
            CancellationToken.None,
            currentUserService: CreateAdminCurrentUser(),
            payoutOrchestrator: null,
            moyasarSettings: null,
            settlementProcessingSettingsService: settlementSettings.Object);

        var result = await controller.ProcessWithdrawal(
            withdrawal.Id,
            new AdminProcessWithdrawalRequest(false, null, "Rejected before bank submission"),
            context,
            null!,
            null!,
            null!,
            Mock.Of<IDriverWalletNotificationService>(),
            CancellationToken.None,
            currentUserService: CreateAdminCurrentUser(),
            payoutOrchestrator: CreatePayoutOrchestrator(context, [], settlementSettings.Object),
            moyasarSettings: null,
            settlementProcessingSettingsService: settlementSettings.Object);

        var response = ((OkObjectResult)result.Result!).Value.Should()
            .BeOfType<AdminProcessWithdrawalResultDto>().Subject;

        withdrawal.Status.Should().Be(DriverWithdrawalStatus.Cancelled);
        context.Payouts.Should().ContainSingle(item => item.Status == PayoutStatus.Cancelled);
        context.Settlements.Should().ContainSingle(item => item.Status == SettlementStatus.OnHold);
        context.WalletHolds.Should().ContainSingle(item =>
            item.ReferenceId == withdrawal.Id && item.Status == WalletHoldStatus.Cancelled);
        response.WithdrawalStatus.Should().Be(DriverWithdrawalStatus.Cancelled.ToString());
        response.PayoutStatus.Should().Be(PayoutStatus.Cancelled.ToString());
    }

    [Fact]
    public async Task ProcessWithdrawal_RejectsApprovalBeforeDriversPreferredPayoutDay()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new AdminWalletsController();
        var driver = CreateApprovedDriver();
        if (SaudiTime.Today.DayOfWeek == DayOfWeek.Monday)
        {
            driver.UpdatePayoutDay(PayoutScheduleDay.Thursday);
        }

        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        wallet.Credit(100m);
        var payoutMethod = new DriverPayoutMethod(
            driver.Id,
            DriverPayoutMethodType.BankAccount,
            "Driver Name",
            "SA1234567890123456789012",
            "Bank",
            true);
        var withdrawal = new DriverWithdrawalRequest(driver.Id, wallet.Id, payoutMethod.Id, 40m);

        context.Drivers.Add(driver);
        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        context.DriverWithdrawalRequests.Add(withdrawal);
        await context.SaveChangesAsync(CancellationToken.None);

        var act = () => controller.ProcessWithdrawal(
            withdrawal.Id,
            new AdminProcessWithdrawalRequest(true, null, null),
            context,
            null!,
            null!,
            null!,
            Mock.Of<IDriverWalletNotificationService>(),
            CancellationToken.None,
            currentUserService: CreateAdminCurrentUser());

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*selected*");
    }

    [Fact]
    public async Task ProcessWithdrawal_AutomaticModeWithoutGateway_DoesNotUseTransferReferenceAsPayment()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new AdminWalletsController();
        var driver = CreateApprovedDriver();
        driver.UpdatePayoutDay((PayoutScheduleDay)SaudiTime.Today.DayOfWeek);
        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        wallet.Credit(100m);
        var payoutMethod = new DriverPayoutMethod(
            driver.Id,
            DriverPayoutMethodType.BankAccount,
            "Driver Name",
            "SA1234567890123456789012",
            "Bank",
            true);
        var withdrawal = new DriverWithdrawalRequest(driver.Id, wallet.Id, payoutMethod.Id, 40m);
        var settlementSettings = new Mock<ISettlementProcessingSettingsService>();
        settlementSettings
            .Setup(service => service.IsAutomaticAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        settlementSettings
            .Setup(service => service.GetEnabledPayoutDaysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([(PayoutScheduleDay)SaudiTime.Today.DayOfWeek]);

        context.Drivers.Add(driver);
        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        context.DriverWithdrawalRequests.Add(withdrawal);
        await context.SaveChangesAsync(CancellationToken.None);

        var act = () => controller.ProcessWithdrawal(
            withdrawal.Id,
            new AdminProcessWithdrawalRequest(true, "bank-reference-must-not-pay", null),
            context,
            null!,
            null!,
            null!,
            Mock.Of<IDriverWalletNotificationService>(),
            CancellationToken.None,
            currentUserService: CreateAdminCurrentUser(),
            payoutOrchestrator: CreatePayoutOrchestrator(context, [], settlementSettings.Object),
            moyasarSettings: null,
            settlementProcessingSettingsService: settlementSettings.Object);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(error => error.ErrorCode == "PAYOUT_GATEWAY_UNAVAILABLE");

        withdrawal.Status.Should().Be(DriverWithdrawalStatus.Pending);
        withdrawal.TransferReference.Should().BeNull();
        context.Settlements.Should().BeEmpty();
        context.Payouts.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessWithdrawal_AddsReleaseTransaction_WhenRejected()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new AdminWalletsController();
        var driverWalletNotificationService = Mock.Of<IDriverWalletNotificationService>();

        var driverId = Guid.NewGuid();
        var wallet = new Wallet(WalletOwnerType.Driver, driverId);
        wallet.Credit(100m);

        var payoutMethod = new DriverPayoutMethod(driverId, DriverPayoutMethodType.BankAccount, "Driver Name", "1234567890", "Bank", true);
        var withdrawal = new DriverWithdrawalRequest(driverId, wallet.Id, payoutMethod.Id, 40m);

        wallet.Hold(withdrawal.Amount);

        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        context.DriverWithdrawalRequests.Add(withdrawal);
        context.WalletTransactions.Add(new WalletTransaction(
            wallet.Id,
            WalletTxnType.Hold,
            withdrawal.Amount,
            "OUT",
            description: "Driver withdrawal request submitted",
            referenceType: "DriverWithdrawal",
            referenceId: withdrawal.Id));
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await controller.ProcessWithdrawal(
            withdrawal.Id,
            new AdminProcessWithdrawalRequest(false, null, "Rejected"),
            context,
            null!,
            null!,
            null!,
            driverWalletNotificationService,
            CancellationToken.None,
            currentUserService: CreateAdminCurrentUser());

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AdminProcessWithdrawalResultDto>().Subject;

        withdrawal.Status.Should().Be(DriverWithdrawalStatus.Failed);
        withdrawal.FailureReason.Should().Be("Rejected");
        response.WithdrawalStatus.Should().Be(DriverWithdrawalStatus.Failed.ToString());
        response.PayoutId.Should().BeNull();
    }

    private static PayoutOrchestrator CreatePayoutOrchestrator(
        IApplicationDbContext context,
        IEnumerable<IPayoutGateway> gateways,
        ISettlementProcessingSettingsService settings)
    {
        return new PayoutOrchestrator(
            context,
            gateways,
            new FinancialEventPostingService(
                context,
                NullLogger<FinancialEventPostingService>.Instance),
            new WalletProjectionUpdater(context),
            new VendorPayoutWalletService(
                context,
                NullLogger<VendorPayoutWalletService>.Instance),
            Options.Create(new FinancialSettingsOptions()),
            Mock.Of<IAdminAlertService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IOneSignalPushService>(),
            Mock.Of<IDriverWalletNotificationService>(),
            settings);
    }

    private static ICurrentUserService CreateAdminCurrentUser()
    {
        var adminId = Guid.NewGuid();
        return Mock.Of<ICurrentUserService>(service => service.UserId == adminId);
    }

    private static Driver CreateApprovedDriver()
    {
        var driver = new Driver(
            Guid.NewGuid(),
            null,
            null,
            null,
            region: "RIYADH",
            city: "RIYADH");
        driver.Approve(Guid.NewGuid(), "approved for payout tests");
        return driver;
    }
}
