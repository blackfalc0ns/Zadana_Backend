using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Api.Modules.Vendors.Controllers;
using Zadana.Api.Modules.Vendors.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Common.Models;
using Zadana.Application.Modules.Vendors.Commands.ApproveVendor;
using Zadana.Application.Modules.Vendors.Commands.ApproveVendorDocumentReview;
using Zadana.Application.Modules.Vendors.Commands.RequestVendorDocuments;
using Zadana.Application.Modules.Vendors.Commands.RejectVendor;
using Zadana.Application.Modules.Vendors.Commands.RejectVendorDocumentReview;
using Zadana.Application.Modules.Vendors.Commands.ReactivateVendor;
using Zadana.Application.Modules.Vendors.Commands.StartVendorReview;
using Zadana.Application.Modules.Vendors.Commands.SuspendVendor;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Application.Modules.Vendors.Queries.GetAllVendors;
using Zadana.Application.Modules.Vendors.Queries.GetVendorDetail;

namespace Zadana.UnitTests.Modules.Vendors.Controllers;

public class AdminVendorsControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<IOneSignalPushService> _oneSignalPushServiceMock = new();
    private readonly Mock<IVendorCommunicationService> _vendorCommunicationServiceMock = new();
    private readonly AdminVendorsController _controller;

    public AdminVendorsControllerTests()
    {
        _localizerMock.Setup(localizer => localizer[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _controller = new AdminVendorsController(
            _localizerMock.Object,
            _contextMock.Object,
            _notificationServiceMock.Object,
            _oneSignalPushServiceMock.Object,
            _vendorCommunicationServiceMock.Object);

        var services = new ServiceCollection();
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
        var items = new List<VendorListItemDto>
        {
            new(Guid.NewGuid(), "Ar", "En", "Retail", "Active", "Owner", "123", DateTime.UtcNow)
        };
        var paginatedList = new PaginatedList<VendorListItemDto>(items, 1, 1, 10);

        _senderMock.Setup(sender => sender.Send(It.IsAny<GetAllVendorsQuery>(), default))
            .ReturnsAsync(paginatedList);

        var result = await _controller.GetAllVendors(null, null, 1, 10);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(paginatedList);
    }

    [Fact]
    public async Task GetVendorDetail_ReturnsOkResult()
    {
        var vendorId = Guid.NewGuid();
        var dto = new VendorDetailDto(
            vendorId,
            "Ar",
            "En",
            "Retail",
            "CR",
            null,
            null,
            null,
            "c@t.com",
            "123",
            null,
            null,
            null,
            null,
            null,
            null,
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
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            false,
            "Owner",
            "o@t.com",
            "123",
            null,
            null,
            null,
            "weekly",
            new VendorOperationsSettingsDto(true, null, null),
            new VendorNotificationSettingsDto(true, false, true),
            null,
            [],
            [],
            [],
            0,
            0);

        _senderMock.Setup(sender => sender.Send(It.Is<GetVendorDetailQuery>(query => query.VendorId == vendorId), default))
            .ReturnsAsync(dto);

        var result = await _controller.GetVendorDetail(vendorId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task ApproveVendor_ReturnsOkResult_WithLocalizedMessage()
    {
        var vendorId = Guid.NewGuid();
        var request = new ApproveVendorRequest(15.5m);

        var result = await _controller.ApproveVendor(vendorId, request);

        _senderMock.Verify(sender => sender.Send(It.Is<ApproveVendorCommand>(command => command.VendorId == vendorId && command.CommissionRate == 15.5m), default), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RejectVendor_ReturnsOkResult_WithLocalizedMessage()
    {
        var vendorId = Guid.NewGuid();
        var request = new RejectVendorRequest("Missing documents");

        var result = await _controller.RejectVendor(vendorId, request);

        _senderMock.Verify(sender => sender.Send(It.Is<RejectVendorCommand>(command => command.VendorId == vendorId && command.Reason == "Missing documents"), default), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SuspendVendor_ReturnsOkResult_WithLocalizedMessage()
    {
        var vendorId = Guid.NewGuid();
        var request = new SuspendVendorRequest("Policy violation");

        var result = await _controller.SuspendVendor(vendorId, request);

        _senderMock.Verify(sender => sender.Send(It.Is<SuspendVendorCommand>(command => command.VendorId == vendorId && command.Reason == "Policy violation"), default), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ReactivateVendor_ReturnsOkResult()
    {
        var vendorId = Guid.NewGuid();

        var result = await _controller.ReactivateVendor(vendorId);

        _senderMock.Verify(sender => sender.Send(It.Is<ReactivateVendorCommand>(command => command.VendorId == vendorId), default), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task StartVendorReview_ReturnsOkResult()
    {
        var vendorId = Guid.NewGuid();

        var result = await _controller.StartVendorReview(vendorId);

        _senderMock.Verify(sender => sender.Send(It.Is<StartVendorReviewCommand>(command => command.VendorId == vendorId), default), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RequestVendorDocuments_ReturnsOkResult()
    {
        var vendorId = Guid.NewGuid();
        var request = new AdminRequestVendorDocumentsRequest("Need fresh tax and license files.");

        var result = await _controller.RequestVendorDocuments(vendorId, request);

        _senderMock.Verify(sender => sender.Send(It.Is<RequestVendorDocumentsCommand>(command =>
            command.VendorId == vendorId && command.Note == request.Note), default), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ApproveVendorDocument_ReturnsOkResult()
    {
        var vendorId = Guid.NewGuid();
        const string documentId = "commercial";

        var result = await _controller.ApproveVendorDocument(vendorId, documentId);

        _senderMock.Verify(sender => sender.Send(It.Is<ApproveVendorDocumentReviewCommand>(command =>
            command.VendorId == vendorId && command.DocumentId == documentId), default), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RejectVendorDocument_ReturnsOkResult()
    {
        var vendorId = Guid.NewGuid();
        const string documentId = "tax";
        var request = new AdminRejectVendorDocumentRequest("Missing official stamp.");

        var result = await _controller.RejectVendorDocument(vendorId, documentId, request);

        _senderMock.Verify(sender => sender.Send(It.Is<RejectVendorDocumentReviewCommand>(command =>
            command.VendorId == vendorId && command.DocumentId == documentId && command.Reason == request.Reason), default), Times.Once);
        result.Should().BeOfType<OkObjectResult>();
    }
}
