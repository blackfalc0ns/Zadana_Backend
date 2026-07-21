using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Zadana.Api.Modules.Delivery.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Api.Modules.Wallets.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Wallets.DTOs;
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
        var oneSignalPushService = Mock.Of<IOneSignalPushService>();
        var act = () => controller.CreateWithdrawal(
            new CreateDriverWithdrawalRequest(Guid.NewGuid(), 20m),
            currentUserService,
            driverRepository.Object,
            context,
            notificationService,
            oneSignalPushService,
            adminAlertService,
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*selected payout method was not found*");
    }

    [Fact]
    public async Task UpdatePayoutPreference_StoresSelectedThursday()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);

        var result = await controller.UpdatePayoutPreference(
            new UpdateDriverPayoutPreferenceRequest("thursday"),
            currentUserService,
            driverRepository.Object,
            context,
            CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(new DriverPayoutPreferenceDto("Thursday"));
        driver.PayoutDay.Should().Be(PayoutScheduleDay.Thursday);
    }

    [Fact]
    public async Task UpdatePayoutPreference_RejectsUnsupportedDay()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = CreateApprovedDriver();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);

        var act = () => controller.UpdatePayoutPreference(
            new UpdateDriverPayoutPreferenceRequest("Sunday"),
            currentUserService,
            driverRepository.Object,
            context,
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Monday or Thursday*");
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
}

public class AdminWalletsControllerTests
{
    [Fact]
    public async Task ProcessWithdrawal_RequiresManualConfirmationEndpoint_WhenManualModeIsEnabled()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new AdminWalletsController();
        var driverId = Guid.NewGuid();
        var wallet = new Wallet(WalletOwnerType.Driver, driverId);
        var payoutMethod = new DriverPayoutMethod(
            driverId,
            DriverPayoutMethodType.BankAccount,
            "Driver Name",
            "SA1234567890123456789012",
            "Bank",
            true);
        var withdrawal = new DriverWithdrawalRequest(driverId, wallet.Id, payoutMethod.Id, 40m);
        var settlementSettings = new Mock<ISettlementProcessingSettingsService>();
        settlementSettings
            .Setup(service => service.IsAutomaticAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        context.Wallets.Add(wallet);
        context.DriverPayoutMethods.Add(payoutMethod);
        context.DriverWithdrawalRequests.Add(withdrawal);
        await context.SaveChangesAsync(CancellationToken.None);

        var act = () => controller.ProcessWithdrawal(
            withdrawal.Id,
            new AdminProcessWithdrawalRequest(true, "legacy-reference", null),
            context,
            null!,
            null!,
            null!,
            Mock.Of<INotificationService>(),
            Mock.Of<IOneSignalPushService>(),
            CancellationToken.None,
            payoutOrchestrator: null,
            moyasarSettings: null,
            settlementProcessingSettingsService: settlementSettings.Object);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*manual confirmation endpoint*");
        context.Payouts.Should().BeEmpty();
        withdrawal.Status.Should().Be(DriverWithdrawalStatus.Pending);
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
            Mock.Of<INotificationService>(),
            Mock.Of<IOneSignalPushService>(),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*selected*");
    }

    [Fact]
    public async Task ProcessWithdrawal_AddsReleaseTransaction_WhenRejected()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new AdminWalletsController();
        var notificationService = Mock.Of<INotificationService>();
        var oneSignalPushService = Mock.Of<IOneSignalPushService>();

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
            notificationService,
            oneSignalPushService,
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        withdrawal.Status.Should().Be(DriverWithdrawalStatus.Failed);
        withdrawal.FailureReason.Should().Be("Rejected");
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
