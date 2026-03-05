using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;
using Zadana.Api.Modules.Vendors.Controllers;
using Zadana.Application.Common.Localization;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Vendors.Commands.ApproveVendor;
using Zadana.Application.Modules.Vendors.Commands.RejectVendor;
using Zadana.Application.Modules.Vendors.Commands.SuspendVendor;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Queries.GetAllVendors;
using Zadana.Application.Modules.Vendors.Queries.GetVendorDetail;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.UnitTests.Modules.Vendors.Controllers;

public class AdminVendorsControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock = new();
    private readonly AdminVendorsController _controller;

    public AdminVendorsControllerTests()
    {
        _localizerMock.Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _controller = new AdminVendorsController(_localizerMock.Object);

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton(_senderMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                RequestServices = serviceProvider
            }
        };
    }

    [Fact]
    public async Task GetAllVendors_ReturnsOkResult()
    {
        // Arrange
        var items = new List<VendorListItemDto>
        {
            new(Guid.NewGuid(), "Ar", "En", "Retail", "Active", "Owner", "123", DateTime.UtcNow)
        };
        var paginatedList = new PaginatedList<VendorListItemDto>(items, 1, 1, 10);

        _senderMock.Setup(x => x.Send(It.IsAny<GetAllVendorsQuery>(), default))
            .ReturnsAsync(paginatedList);

        // Act
        var result = await _controller.GetAllVendors(null, null, 1, 10);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(paginatedList);
    }

    [Fact]
    public async Task GetVendorDetail_ReturnsOkResult()
    {
        // Arrange
        var vendorId = Guid.NewGuid();
        var dto = new VendorDetailDto(vendorId, "Ar", "En", "Retail", "CR", null, "c@t.com", "123", null, "Active", null, null, null, null, null, DateTime.UtcNow, "Owner", "o@t.com", "123", 0, 0);

        _senderMock.Setup(x => x.Send(It.Is<GetVendorDetailQuery>(q => q.VendorId == vendorId), default))
            .ReturnsAsync(dto);

        // Act
        var result = await _controller.GetVendorDetail(vendorId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task ApproveVendor_ReturnsOkResult_WithLocalizedMessage()
    {
        // Arrange
        var vendorId = Guid.NewGuid();
        var request = new ApproveVendorRequest(15.5m);

        // Act
        var result = await _controller.ApproveVendor(vendorId, request);

        // Assert
        _senderMock.Verify(x => x.Send(It.Is<ApproveVendorCommand>(c => c.VendorId == vendorId && c.CommissionRate == 15.5m), default), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RejectVendor_ReturnsOkResult_WithLocalizedMessage()
    {
        // Arrange
        var vendorId = Guid.NewGuid();
        var request = new RejectVendorRequest("Missing documents");

        // Act
        var result = await _controller.RejectVendor(vendorId, request);

        // Assert
        _senderMock.Verify(x => x.Send(It.Is<RejectVendorCommand>(c => c.VendorId == vendorId && c.Reason == "Missing documents"), default), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SuspendVendor_ReturnsOkResult_WithLocalizedMessage()
    {
        // Arrange
        var vendorId = Guid.NewGuid();
        var request = new SuspendVendorRequest("Policy violation");

        // Act
        var result = await _controller.SuspendVendor(vendorId, request);

        // Assert
        _senderMock.Verify(x => x.Send(It.Is<SuspendVendorCommand>(c => c.VendorId == vendorId && c.Reason == "Policy violation"), default), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }
}
