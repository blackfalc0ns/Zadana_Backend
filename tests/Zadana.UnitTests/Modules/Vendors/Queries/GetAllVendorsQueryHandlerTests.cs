using FluentAssertions;
using Moq;
using Xunit;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Queries.GetAllVendors;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Vendors.Queries;

public class GetAllVendorsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllVendorsPaginated()
    {
        // Arrange
        var db = TestDbContextFactory.Create();

        var userId1 = Guid.NewGuid();
        var user1 = new User("Owner 1", "owner1@test.com", "111", UserRole.Vendor);
        typeof(User).GetProperty("Id")!.SetValue(user1, userId1);
        db.Users.Add(user1);

        var vendor1 = new Vendor(userId1, "Ar 1", "En 1", "Retail", "CR1", "t1@t.com", "111");
        vendor1.Approve(10, Guid.NewGuid());
        db.Vendors.Add(vendor1);

        var userId2 = Guid.NewGuid();
        var user2 = new User("Owner 2", "owner2@test.com", "222", UserRole.Vendor);
        typeof(User).GetProperty("Id")!.SetValue(user2, userId2);
        db.Users.Add(user2);

        var vendor2 = new Vendor(userId2, "Ar 2", "En 2", "Wholesale", "CR2", "t2@t.com", "222");
        db.Vendors.Add(vendor2);

        await db.SaveChangesAsync();

        var handler = new GetAllVendorsQueryHandler(db);

        // Act - All vendors
        var result = await handler.Handle(new GetAllVendorsQuery(null, null, 1, 10), default);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsFilteredVendors()
    {
        // Arrange
        var db = TestDbContextFactory.Create();

        var userId1 = Guid.NewGuid();
        var user1 = new User("Owner 1", "owner1@test.com", "111", UserRole.Vendor);
        typeof(User).GetProperty("Id")!.SetValue(user1, userId1);
        db.Users.Add(user1);

        var vendor1 = new Vendor(userId1, "Ar 1", "En 1", "Retail", "CR1", "t1@t.com", "111");
        vendor1.Approve(10, Guid.NewGuid());
        db.Vendors.Add(vendor1);

        var userId2 = Guid.NewGuid();
        var user2 = new User("Owner 2", "owner2@test.com", "222", UserRole.Vendor);
        typeof(User).GetProperty("Id")!.SetValue(user2, userId2);
        db.Users.Add(user2);

        var vendor2 = new Vendor(userId2, "Ar 2", "En 2", "Wholesale", "CR2", "t2@t.com", "222");
        db.Vendors.Add(vendor2);

        await db.SaveChangesAsync();

        var handler = new GetAllVendorsQueryHandler(db);

        // Act - Active only
        var result = await handler.Handle(new GetAllVendorsQuery(VendorStatus.Active, null, 1, 10), default);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.First().Id.Should().Be(vendor1.Id);
    }
}
