using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorPayoutPreference;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class UpdateVendorPayoutPreferenceCommandHandlerTests
{
    [Fact]
    public async Task Handle_UpdatesPayoutDayWithoutRequiringBankingData()
    {
        var userId = Guid.NewGuid();
        var vendor = CreateVendorWithoutBankAccounts(userId);
        var vendorRepository = new Mock<IVendorRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUserService>();
        var settlementSettings = new Mock<ISettlementProcessingSettingsService>();

        currentUser.SetupGet(service => service.UserId).Returns(userId);
        vendorRepository
            .Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        settlementSettings
            .Setup(service => service.EnsurePayoutDayEnabledAsync(
                PayoutScheduleDay.Thursday,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        settlementSettings
            .Setup(service => service.GetEnabledPayoutDaysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([PayoutScheduleDay.Monday, PayoutScheduleDay.Thursday]);

        var handler = new UpdateVendorPayoutPreferenceCommandHandler(
            vendorRepository.Object,
            unitOfWork.Object,
            currentUser.Object,
            settlementSettings.Object);

        var result = await handler.Handle(
            new UpdateVendorPayoutPreferenceCommand("thursday"),
            CancellationToken.None);

        result.PayoutDay.Should().Be("Thursday");
        result.AvailablePayoutDays.Should().BeEquivalentTo(["Monday", "Thursday"]);
        vendor.PayoutDay.Should().Be(PayoutScheduleDay.Thursday);
        vendor.BankAccounts.Should().BeEmpty();
        unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectsInvalidWeekday()
    {
        var handler = new UpdateVendorPayoutPreferenceCommandHandler(
            Mock.Of<IVendorRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ICurrentUserService>(),
            Mock.Of<ISettlementProcessingSettingsService>());

        var action = () => handler.Handle(
            new UpdateVendorPayoutPreferenceCommand("Funday"),
            CancellationToken.None);

        await action.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*valid day of the week*");
    }

    private static Vendor CreateVendorWithoutBankAccounts(Guid userId) => new(
        userId,
        "متجر اختبار",
        "Test vendor",
        "Retail",
        "CR-TEST",
        "vendor@example.test",
        "0500000000");
}
