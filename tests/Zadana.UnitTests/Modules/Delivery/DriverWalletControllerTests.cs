using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Zadana.Api.Modules.Delivery.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Api.Modules.Wallets.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Wallets.DTOs;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Delivery;

public class DriverWalletControllerTests
{
    [Fact]
    public async Task CreatePaymentMethod_ThrowsBadRequestException_WhenRequestBodyIsMissing()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = new Driver(Guid.NewGuid(), null, null, null);
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);

        var act = () => controller.CreatePaymentMethod(
            null,
            currentUserService,
            driverRepository.Object,
            context,
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Request body is required.");
    }

    [Fact]
    public async Task CreatePaymentMethod_ThrowsBadRequestException_WhenAccountIdentifierIsBlank()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = new Driver(Guid.NewGuid(), null, null, null);
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);

        var act = () => controller.CreatePaymentMethod(
            new CreateDriverPayoutMethodRequest("BankAccount", "Driver Name", " ", "Bank", true),
            currentUserService,
            driverRepository.Object,
            context,
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Account identifier is required.");
    }

    [Fact]
    public async Task DeletePaymentMethod_ThrowsBusinessRuleException_WhenMethodHasWithdrawalHistory()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var driver = new Driver(Guid.NewGuid(), null, null, null);
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);

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
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*linked to withdrawal requests*");
    }

    [Fact]
    public async Task CreateWithdrawal_ThrowsBusinessRuleException_WhenSpecifiedPaymentMethodDoesNotBelongToDriver()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new DriverWalletController();
        var notificationService = Mock.Of<INotificationService>();
        var driver = new Driver(Guid.NewGuid(), null, null, null);
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == driver.UserId);
        var driverRepository = CreateDriverRepository(driver);

        var wallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        wallet.Credit(100m);
        context.Wallets.Add(wallet);
        await context.SaveChangesAsync(CancellationToken.None);

        var act = () => controller.CreateWithdrawal(
            new CreateDriverWithdrawalRequest(Guid.NewGuid(), 20m),
            currentUserService,
            driverRepository.Object,
            context,
            notificationService,
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*selected payout method was not found*");
    }

    private static Mock<IDriverRepository> CreateDriverRepository(Driver driver)
    {
        var repository = new Mock<IDriverRepository>();
        repository
            .Setup(item => item.GetByUserIdAsync(driver.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(driver);
        return repository;
    }
}

public class AdminWalletsControllerTests
{
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
}
