using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.ApproveVendor;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class ApproveVendorCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IVendorRepository> _vendorRepositoryMock = new();
    private readonly Mock<IIdentityAccountService> _identityAccountServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task Handle_WithValidRequest_ApprovesVendor()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "test@test.com", "123");
        var adminId = Guid.NewGuid();

        _currentUserServiceMock.Setup(c => c.UserId).Returns(adminId);
        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);
        _identityAccountServiceMock
            .Setup(service => service.ActivateAsync(vendor.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityOperationResult(true));
        _identityAccountServiceMock
            .Setup(service => service.UnlockLoginAsync(vendor.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityOperationResult(true));

        var handler = new ApproveVendorCommandHandler(
            _vendorRepositoryMock.Object,
            _identityAccountServiceMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);

        await handler.Handle(new ApproveVendorCommand(vendor.Id, 10.5m), default);

        vendor.Status.Should().Be(VendorStatus.Active);
        vendor.CommissionRate.Should().Be(10.5m);
        vendor.ApprovedBy.Should().Be(adminId);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidVendorId_ThrowsNotFoundException()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns(Guid.NewGuid());
        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vendor?)null);

        var handler = new ApproveVendorCommandHandler(
            _vendorRepositoryMock.Object,
            _identityAccountServiceMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ApproveVendorCommand(Guid.NewGuid(), 10), default));
    }

    [Fact]
    public async Task Handle_WithoutAuthenticatedUser_ThrowsUnauthorizedException()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "t@t.com", "1");

        _currentUserServiceMock.Setup(c => c.UserId).Returns((Guid?)null);
        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        var handler = new ApproveVendorCommandHandler(
            _vendorRepositoryMock.Object,
            _identityAccountServiceMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new ApproveVendorCommand(vendor.Id, 10), default));
    }
}
