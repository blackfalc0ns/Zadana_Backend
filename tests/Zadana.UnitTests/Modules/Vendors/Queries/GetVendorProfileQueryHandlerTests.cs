using FluentAssertions;
using Moq;
using Xunit;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Queries.GetVendorProfile;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Vendors.Queries;

public class GetVendorProfileQueryHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsProfileDto()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        _currentUserMock.Setup(c => c.UserId).Returns(userId);

        var user = new User("Owner", "owner@test.com", "123", "hash", UserRole.Vendor);
        typeof(User).GetProperty("Id")!.SetValue(user, userId);
        db.Users.Add(user);

        var vendor = new Vendor(userId, "Business Ar", "Business En", "Retail", "CR001", "contact@test.com", "999");
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();

        var handler = new GetVendorProfileQueryHandler(db, _currentUserMock.Object);

        // Act
        var result = await handler.Handle(new GetVendorProfileQuery(), default);

        // Assert
        result.Should().NotBeNull();
        result.BusinessNameEn.Should().Be("Business En");
        result.ContactEmail.Should().Be("contact@test.com");
    }

    [Fact]
    public async Task Handle_WithoutAuthenticatedUser_ThrowsUnauthorizedException()
    {
        var db = TestDbContextFactory.Create();
        _currentUserMock.Setup(c => c.UserId).Returns((Guid?)null);
        var handler = new GetVendorProfileQueryHandler(db, _currentUserMock.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new GetVendorProfileQuery(), default));
    }

    [Fact]
    public async Task Handle_WhenUserNotVendor_ThrowsNotFoundException()
    {
        var db = TestDbContextFactory.Create();
        _currentUserMock.Setup(c => c.UserId).Returns(Guid.NewGuid());
        var handler = new GetVendorProfileQueryHandler(db, _currentUserMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetVendorProfileQuery(), default));
    }
}
