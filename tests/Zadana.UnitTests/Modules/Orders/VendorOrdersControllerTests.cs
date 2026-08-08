using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Zadana.Api.Modules.Orders.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Queries.GetVendorOrderDetail;

namespace Zadana.UnitTests.Modules.Orders;

public class VendorOrdersControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ICurrentVendorService> _currentVendorServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly VendorOrdersController _controller;

    public VendorOrdersControllerTests()
    {
        _controller = new VendorOrdersController(
            _currentVendorServiceMock.Object,
            _currentUserServiceMock.Object);

        var services = new ServiceCollection();
        services.AddSingleton(_senderMock.Object);
        var provider = services.BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = provider
            }
        };
    }

    [Fact]
    public async Task GetOrderById_ShouldReturnTrackingLocations()
    {
        var vendorId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var vendorLocation = new GeoPointDto(24.7136m, 46.6753m);
        var customerLocation = new GeoPointDto(24.7743m, 46.7386m);
        var driverLiveLocation = new DriverLiveLocationDto(24.7441m, 46.7042m, 8.5m, DateTime.UtcNow);

        _currentVendorServiceMock
            .Setup(x => x.GetRequiredVendorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentVendorScope(vendorId, null));

        var detail = new VendorOrderDetailDto(
            orderId,
            "ORD-1001",
            "Customer",
            "+966500000000",
            "Customer address",
            Guid.NewGuid(),
            "OUT_FOR_DELIVERY",
            "PAID",
            "CASH",
            "Delivery",
            100m,
            12m,
            CreateDeliveryBreakdown(),
            112m,
            null,
            DateTime.UtcNow,
            null,
            "EN_ROUTE",
            DateTime.UtcNow,
            null,
            false,
            "UNAVAILABLE",
            0,
            null,
            null,
            null,
            null,
            [],
            vendorLocation,
            customerLocation,
            driverLiveLocation,
            [],
            []);

        _senderMock
            .Setup(x => x.Send(It.IsAny<GetVendorOrderDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var result = await _controller.GetOrderById(orderId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<VendorOrderDetailResponse>().Subject;
        response.VendorLocation.Should().Be(vendorLocation);
        response.CustomerLocation.Should().Be(customerLocation);
        response.DriverLiveLocation.Should().Be(driverLiveLocation);

        _senderMock.Verify(x => x.Send(
            It.Is<GetVendorOrderDetailQuery>(query => query.VendorId == vendorId && query.OrderId == orderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static OrderDeliveryBreakdownDto CreateDeliveryBreakdown() =>
        new(
            0m,
            0m,
            0m,
            0m,
            0m,
            "none",
            "none",
            "none",
            false,
            "not_quoted",
            null,
            null,
            null,
            0,
            false,
            null,
            null);
}
