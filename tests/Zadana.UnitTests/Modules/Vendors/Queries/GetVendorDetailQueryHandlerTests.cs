using FluentAssertions;
using Moq;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Queries.GetVendorDetail;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Queries;

public class GetVendorDetailQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithValidId_ReturnsDetailDto()
    {
        var vendorId = Guid.NewGuid();
        var expected = new VendorDetailDto(
            vendorId,
            "Business Ar",
            "Business En",
            "Retail",
            "CR001",
            null,
            null,
            null,
            "contact@test.com",
            "999",
            null,
            null,
            null,
            null,
            null,
            10m,
            "Active",
            "Active",
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "Owner Name",
            "owner@test.com",
            "123",
            null,
            null,
            null,
            new VendorOperationsSettingsDto(true, null, null),
            new VendorNotificationSettingsDto(true, false, true),
            null,
            [],
            [],
            2,
            1);

        var readService = new Mock<IVendorReadService>();
        readService
            .Setup(service => service.GetDetailAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetVendorDetailQueryHandler(readService.Object);

        var result = await handler.Handle(new GetVendorDetailQuery(vendorId), default);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Handle_WithInvalidId_ThrowsNotFoundException()
    {
        var vendorId = Guid.NewGuid();
        var readService = new Mock<IVendorReadService>();
        readService
            .Setup(service => service.GetDetailAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorDetailDto?)null);

        var handler = new GetVendorDetailQueryHandler(readService.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetVendorDetailQuery(vendorId), default));
    }
}
