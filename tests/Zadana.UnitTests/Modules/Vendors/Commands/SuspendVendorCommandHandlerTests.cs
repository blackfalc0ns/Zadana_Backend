using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.SuspendVendor;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class SuspendVendorCommandHandlerTests
{
    private readonly Mock<IVendorRepository> _vendorRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task Handle_WithActiveVendor_SuspendsVendor()
    {
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "t@t.com", "1");
        vendor.Approve(10, Guid.NewGuid());
        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(vendor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        var handler = new SuspendVendorCommandHandler(_vendorRepositoryMock.Object, _unitOfWorkMock.Object);

        await handler.Handle(new SuspendVendorCommand(vendor.Id, "Policy violation"), default);

        vendor.Status.Should().Be(VendorStatus.Suspended);
        vendor.RejectionReason.Should().Be("Policy violation");
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidVendorId_ThrowsNotFoundException()
    {
        _vendorRepositoryMock
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vendor?)null);

        var handler = new SuspendVendorCommandHandler(_vendorRepositoryMock.Object, _unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SuspendVendorCommand(Guid.NewGuid(), "reason"), default));
    }
}
