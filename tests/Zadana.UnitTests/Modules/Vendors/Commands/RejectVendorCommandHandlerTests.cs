using FluentAssertions;
using Moq;
using Xunit;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Vendors.Commands.RejectVendor;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Vendors.Commands;

public class RejectVendorCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidRequest_RejectsVendor()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var vendor = new Vendor(Guid.NewGuid(), "Ar", "En", "Retail", "CR", "t@t.com", "1");
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();

        var handler = new RejectVendorCommandHandler(db);

        // Act
        await handler.Handle(new RejectVendorCommand(vendor.Id, "Missing docs"), default);

        // Assert
        var updated = await db.Vendors.FindAsync(vendor.Id);
        updated!.Status.Should().Be(VendorStatus.Rejected);
        updated.RejectionReason.Should().Be("Missing docs");
    }

    [Fact]
    public async Task Handle_WithInvalidVendorId_ThrowsNotFoundException()
    {
        var db = TestDbContextFactory.Create();
        var handler = new RejectVendorCommandHandler(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new RejectVendorCommand(Guid.NewGuid(), "reason"), default));
    }
}
