using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.AddVendorBankAccount;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class AddVendorBankAccountCommandHandlerTests
{
    [Fact]
    public async Task Handle_SubmitsBankingApprovalRequest_WithoutAddingBankAccount()
    {
        var vendorUserId = Guid.NewGuid();
        var requesterUserId = Guid.NewGuid();
        var approvalRequestId = Guid.NewGuid();
        var vendor = new Vendor(vendorUserId, "متجر", "Store", "Retail", "CR-1", "store@test.com", "123");
        var vendorRepository = new Mock<IVendorRepository>();
        var currentUserService = new Mock<ICurrentUserService>();
        var profileChangeApprovalService = new Mock<IProfileChangeApprovalService>();

        vendorRepository
            .Setup(repository => repository.GetByIdAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        currentUserService.Setup(service => service.UserId).Returns(requesterUserId);
        profileChangeApprovalService
            .Setup(service => service.SubmitAsync(
                requesterUserId,
                vendor.UserId,
                ProfileChangeApprovalActions.VendorProfileBanking,
                It.IsAny<string>(),
                It.IsAny<VendorBankingProfileChangePayload>(),
                It.IsAny<ProfileChangeApprovalAlert>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(approvalRequestId);

        var handler = new AddVendorBankAccountCommandHandler(
            vendorRepository.Object,
            currentUserService.Object,
            profileChangeApprovalService.Object);

        var result = await handler.Handle(
            new AddVendorBankAccountCommand(
                vendor.Id,
                "Bank",
                "Owner",
                "SA1234567890123456789012",
                null,
                null,
                true),
            CancellationToken.None);

        result.Should().Be(approvalRequestId);
        vendorRepository.Verify(repository => repository.AddBankAccount(It.IsAny<VendorBankAccount>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenVendorDoesNotExist_ThrowsNotFoundException()
    {
        var vendorRepository = new Mock<IVendorRepository>();
        var handler = new AddVendorBankAccountCommandHandler(
            vendorRepository.Object,
            Mock.Of<ICurrentUserService>(),
            Mock.Of<IProfileChangeApprovalService>());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new AddVendorBankAccountCommand(
                    Guid.NewGuid(),
                    "Bank",
                    "Owner",
                    "SA1234567890123456789012",
                    null,
                    null,
                    true),
                CancellationToken.None));
    }
}
