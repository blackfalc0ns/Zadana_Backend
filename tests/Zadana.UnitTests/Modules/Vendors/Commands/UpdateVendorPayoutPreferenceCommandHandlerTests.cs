using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
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

        currentUser.SetupGet(service => service.UserId).Returns(userId);
        vendorRepository
            .Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        var handler = new UpdateVendorPayoutPreferenceCommandHandler(
            vendorRepository.Object,
            unitOfWork.Object,
            currentUser.Object);

        var result = await handler.Handle(
            new UpdateVendorPayoutPreferenceCommand("thursday"),
            CancellationToken.None);

        result.PayoutDay.Should().Be("Thursday");
        vendor.PayoutDay.Should().Be(PayoutScheduleDay.Thursday);
        vendor.BankAccounts.Should().BeEmpty();
        unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectsAnyDayOtherThanMondayOrThursday()
    {
        var handler = new UpdateVendorPayoutPreferenceCommandHandler(
            Mock.Of<IVendorRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ICurrentUserService>());

        var action = () => handler.Handle(
            new UpdateVendorPayoutPreferenceCommand("Sunday"),
            CancellationToken.None);

        await action.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Monday or Thursday*");
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
