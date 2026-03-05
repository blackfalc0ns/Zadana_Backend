using FluentAssertions;
using Moq;
using Xunit;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.ApproveVendor;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class ApproveVendorCommandHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

    [Fact]
    public async Task Handle_WithValidRequest_ApprovesVendor()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        _currentUserServiceMock.Setup(c => c.UserId).Returns(adminId);

        var user = new User("Owner", "owner@test.com", "123", "hash", UserRole.Vendor);
        typeof(User).GetProperty("Id")!.SetValue(user, userId);
        db.Users.Add(user);

        var vendor = new Vendor(userId, "Ar", "En", "Retail", "CR", "test@test.com", "123");
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();

        var handler = new ApproveVendorCommandHandler(db, _currentUserServiceMock.Object);
        var command = new ApproveVendorCommand(vendor.Id, 10.5m);

        // Act
        await handler.Handle(command, default);

        // Assert
        var updated = await db.Vendors.FindAsync(vendor.Id);
        updated!.Status.Should().Be(VendorStatus.Active);
        updated.CommissionRate.Should().Be(10.5m);
        updated.ApprovedBy.Should().Be(adminId);
    }

    [Fact]
    public async Task Handle_WithInvalidVendorId_ThrowsNotFoundException()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        _currentUserServiceMock.Setup(c => c.UserId).Returns(Guid.NewGuid());
        var handler = new ApproveVendorCommandHandler(db, _currentUserServiceMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ApproveVendorCommand(Guid.NewGuid(), 10), default));
    }

    [Fact]
    public async Task Handle_WithoutAuthenticatedUser_ThrowsUnauthorizedException()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "t@t.com", "1");
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();

        _currentUserServiceMock.Setup(c => c.UserId).Returns((Guid?)null);
        var handler = new ApproveVendorCommandHandler(db, _currentUserServiceMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new ApproveVendorCommand(vendor.Id, 10), default));
    }
}
