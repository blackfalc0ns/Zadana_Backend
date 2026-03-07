using FluentAssertions;
using Moq;
using Xunit;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Queries.GetVendorDetail;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Vendors.Queries;

public class GetVendorDetailQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithValidId_ReturnsDetailDto()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var user = new User("Owner Name", "owner@test.com", "123", UserRole.Vendor);
        typeof(User).GetProperty("Id")!.SetValue(user, userId);
        db.Users.Add(user);

        var vendor = new Vendor(userId, "Business Ar", "Business En", "Retail", "CR001", "contact@test.com", "999");
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();

        var handler = new GetVendorDetailQueryHandler(db);

        // Act
        var result = await handler.Handle(new GetVendorDetailQuery(vendor.Id), default);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(vendor.Id);
        result.BusinessNameEn.Should().Be("Business En");
        result.OwnerName.Should().Be("Owner Name");
        result.OwnerEmail.Should().Be("owner@test.com");
    }

    [Fact]
    public async Task Handle_WithInvalidId_ThrowsNotFoundException()
    {
        var db = TestDbContextFactory.Create();
        var handler = new GetVendorDetailQueryHandler(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetVendorDetailQuery(Guid.NewGuid()), default));
    }
}
