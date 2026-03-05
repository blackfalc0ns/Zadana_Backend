using FluentAssertions;
using Moq;
using Xunit;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.SuspendVendor;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class SuspendVendorCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithActiveVendor_SuspendsVendor()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "t@t.com", "1");
        vendor.Approve(10, Guid.NewGuid()); // Must be Active to suspend
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();

        var handler = new SuspendVendorCommandHandler(db);

        // Act
        await handler.Handle(new SuspendVendorCommand(vendor.Id, "Policy violation"), default);

        // Assert
        var updated = await db.Vendors.FindAsync(vendor.Id);
        updated!.Status.Should().Be(VendorStatus.Suspended);
        updated.RejectionReason.Should().Be("Policy violation");
    }

    [Fact]
    public async Task Handle_WithInvalidVendorId_ThrowsNotFoundException()
    {
        var db = TestDbContextFactory.Create();
        var handler = new SuspendVendorCommandHandler(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SuspendVendorCommand(Guid.NewGuid(), "reason"), default));
    }
}
