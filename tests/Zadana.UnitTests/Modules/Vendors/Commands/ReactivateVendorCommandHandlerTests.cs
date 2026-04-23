using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.ReactivateVendor;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class ReactivateVendorCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IVendorReviewAuditService> _vendorReviewAuditServiceMock = new();
    private readonly Mock<IVendorRepository> _vendorRepositoryMock = new();
    private readonly Mock<IIdentityAccountService> _identityAccountServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task Handle_WithSuspendedVendor_ReactivatesVendorToActive()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "t@t.com", "1");
        vendor.Approve(10, Guid.NewGuid());
        vendor.Suspend("Policy review");

        _currentUserServiceMock.Setup(service => service.UserId).Returns(Guid.NewGuid());
        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        _identityAccountServiceMock
            .Setup(service => service.ActivateAsync(vendor.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityOperationResult(true));

        var handler = new ReactivateVendorCommandHandler(
            _vendorRepositoryMock.Object,
            _identityAccountServiceMock.Object,
            _vendorReviewAuditServiceMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);

        await handler.Handle(new ReactivateVendorCommand(vendor.Id), default);

        vendor.Status.Should().Be(VendorStatus.Active);
        vendor.SuspensionReason.Should().BeNull();
        vendor.SuspendedAtUtc.Should().BeNull();
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenVendorIsNotSuspended_ThrowsBusinessRuleException()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "t@t.com", "1");

        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        var handler = new ReactivateVendorCommandHandler(
            _vendorRepositoryMock.Object,
            _identityAccountServiceMock.Object,
            _vendorReviewAuditServiceMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new ReactivateVendorCommand(vendor.Id), default));

        exception.Message.Should().Contain("reactivated");
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
