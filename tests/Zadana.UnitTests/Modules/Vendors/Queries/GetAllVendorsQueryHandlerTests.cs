using FluentAssertions;
using Moq;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Queries.GetAllVendors;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.UnitTests.Modules.Vendors.Queries;

public class GetAllVendorsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPaginatedVendors_FromReadService()
    {
        var readService = new Mock<IVendorReadService>();
        var expected = new PaginatedList<VendorListItemDto>(
            [
                new VendorListItemDto(Guid.NewGuid(), "Ar 1", "En 1", "Retail", "Active", "Owner 1", "111", DateTime.UtcNow),
                new VendorListItemDto(Guid.NewGuid(), "Ar 2", "En 2", "Wholesale", "PendingReview", "Owner 2", "222", DateTime.UtcNow)
            ],
            totalCount: 2,
            page: 1,
            pageSize: 10);

        readService
            .Setup(service => service.GetAllAsync(null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetAllVendorsQueryHandler(readService.Object);

        var result = await handler.Handle(new GetAllVendorsQuery(null, null, 1, 10), default);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Handle_PassesStatusAndSearch_ToReadService()
    {
        var readService = new Mock<IVendorReadService>();
        var expected = new PaginatedList<VendorListItemDto>([], 0, 1, 10);

        readService
            .Setup(service => service.GetAllAsync(VendorStatus.Active, "owner", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetAllVendorsQueryHandler(readService.Object);

        var result = await handler.Handle(new GetAllVendorsQuery(VendorStatus.Active, "owner", 1, 10), default);

        result.Should().BeSameAs(expected);
    }
}
