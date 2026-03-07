using FluentAssertions;
using Moq;
using Xunit;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorProfile;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class UpdateVendorProfileCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();

    [Fact]
    public async Task Handle_WithValidRequest_UpdatesProfileAndReturnsDto()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        _currentUserMock.Setup(c => c.UserId).Returns(userId);

        var user = new User("Owner", "owner@test.com", "123", UserRole.Vendor);
        typeof(User).GetProperty("Id")!.SetValue(user, userId);
        db.Users.Add(user);

        var vendor = new Vendor(userId, "Old Ar", "Old En", "Retail", "CR", "old@test.com", "123");
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();

        var handler = new UpdateVendorProfileCommandHandler(db, _currentUserMock.Object);
        var command = new UpdateVendorProfileCommand("New Ar", "New En", "Wholesale", "new@test.com", "999", "Tax123");

        // Act
        var result = await handler.Handle(command, default);

        // Assert
        result.Should().NotBeNull();
        result.BusinessNameEn.Should().Be("New En");
        result.TaxId.Should().Be("Tax123");
        result.ContactEmail.Should().Be("new@test.com");
    }

    [Fact]
    public async Task Handle_WhenUserNotVendor_ThrowsNotFoundException()
    {
        var db = TestDbContextFactory.Create();
        _currentUserMock.Setup(c => c.UserId).Returns(Guid.NewGuid());
        var handler = new UpdateVendorProfileCommandHandler(db, _currentUserMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UpdateVendorProfileCommand("A", "B", "C", "d@e.com", "1", null), default));
    }

    [Fact]
    public async Task Handle_WithoutAuthenticatedUser_ThrowsUnauthorizedException()
    {
        var db = TestDbContextFactory.Create();
        _currentUserMock.Setup(c => c.UserId).Returns((Guid?)null);
        var handler = new UpdateVendorProfileCommandHandler(db, _currentUserMock.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new UpdateVendorProfileCommand("A", "B", "C", "d@e.com", "1", null), default));
    }
}
