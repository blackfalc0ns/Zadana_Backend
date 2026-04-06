using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Queries.GetVendorProfile;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Queries;

public class GetVendorProfileQueryHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsProfileDto()
    {
        var userId = Guid.NewGuid();
        var expected = new VendorProfileDto(
            Guid.NewGuid(),
            "Business Ar",
            "Business En",
            "Retail",
            "CR001",
            null,
            "contact@test.com",
            "999",
            10,
            "Active",
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _currentUserMock.Setup(currentUser => currentUser.UserId).Returns(userId);

        var readService = new Mock<IVendorReadService>();
        readService
            .Setup(service => service.GetProfileByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetVendorProfileQueryHandler(readService.Object, _currentUserMock.Object);

        var result = await handler.Handle(new GetVendorProfileQuery(), default);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Handle_WithoutAuthenticatedUser_ThrowsUnauthorizedException()
    {
        _currentUserMock.Setup(currentUser => currentUser.UserId).Returns((Guid?)null);

        var readService = new Mock<IVendorReadService>();
        var handler = new GetVendorProfileQueryHandler(readService.Object, _currentUserMock.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new GetVendorProfileQuery(), default));
    }

    [Fact]
    public async Task Handle_WhenUserNotVendor_ThrowsNotFoundException()
    {
        var userId = Guid.NewGuid();
        _currentUserMock.Setup(currentUser => currentUser.UserId).Returns(userId);

        var readService = new Mock<IVendorReadService>();
        readService
            .Setup(service => service.GetProfileByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorProfileDto?)null);

        var handler = new GetVendorProfileQueryHandler(readService.Object, _currentUserMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetVendorProfileQuery(), default));
    }
}
