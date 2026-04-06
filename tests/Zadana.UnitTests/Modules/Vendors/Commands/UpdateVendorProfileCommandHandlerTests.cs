using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorProfile;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class UpdateVendorProfileCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IVendorRepository> _vendorRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task Handle_WithValidRequest_UpdatesProfileAndReturnsDto()
    {
        var userId = Guid.NewGuid();
        _currentUserMock.Setup(c => c.UserId).Returns(userId);

        var vendor = new Vendor(userId, "Old Ar", "Old En", "Retail", "CR", "old@test.com", "123");
        _vendorRepositoryMock
            .Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        var handler = new UpdateVendorProfileCommandHandler(
            _vendorRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserMock.Object);

        var result = await handler.Handle(
            new UpdateVendorProfileCommand("New Ar", "New En", "Wholesale", "new@test.com", "999", "Tax123"),
            default);

        result.BusinessNameEn.Should().Be("New En");
        result.TaxId.Should().Be("Tax123");
        result.ContactEmail.Should().Be("new@test.com");
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserNotVendor_ThrowsNotFoundException()
    {
        var userId = Guid.NewGuid();
        _currentUserMock.Setup(c => c.UserId).Returns(userId);
        _vendorRepositoryMock
            .Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vendor?)null);

        var handler = new UpdateVendorProfileCommandHandler(
            _vendorRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UpdateVendorProfileCommand("A", "B", "C", "d@e.com", "1", null), default));
    }

    [Fact]
    public async Task Handle_WithoutAuthenticatedUser_ThrowsUnauthorizedException()
    {
        _currentUserMock.Setup(c => c.UserId).Returns((Guid?)null);

        var handler = new UpdateVendorProfileCommandHandler(
            _vendorRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _currentUserMock.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new UpdateVendorProfileCommand("A", "B", "C", "d@e.com", "1", null), default));
    }
}
