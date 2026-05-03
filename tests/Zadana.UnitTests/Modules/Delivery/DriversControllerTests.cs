using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Zadana.Api.Modules.Delivery.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Commands.ResendAssignmentOtp;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Delivery;

public class DriversControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly DriversController _controller;

    public DriversControllerTests()
    {
        _controller = new DriversController();

        var services = new ServiceCollection();
        services.AddSingleton(_senderMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider()
            }
        };
    }

    [Fact]
    public async Task ResendOtp_ThrowsBadRequestException_WhenRequestBodyIsMissing()
    {
        var currentUserService = Mock.Of<ICurrentUserService>(service =>
            service.UserId == Guid.NewGuid());

        var act = () => _controller.ResendOtp(Guid.NewGuid(), null, currentUserService, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Request body is required.");
    }

    [Fact]
    public async Task ResendOtp_ThrowsBadRequestException_WhenOtpTypeIsBlank()
    {
        var currentUserService = Mock.Of<ICurrentUserService>(service =>
            service.UserId == Guid.NewGuid());

        var act = () => _controller.ResendOtp(
            Guid.NewGuid(),
            new DriverResendOtpRequest(" "),
            currentUserService,
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("OTP type is required.");
    }

    [Fact]
    public async Task ResendOtp_ReturnsOkResult_WhenRequestIsValid()
    {
        var userId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var currentUserService = Mock.Of<ICurrentUserService>(service => service.UserId == userId);
        ResendAssignmentOtpCommand? sentCommand = null;

        _senderMock
            .Setup(sender => sender.Send(It.IsAny<ResendAssignmentOtpCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest, CancellationToken>((request, _) => sentCommand = (ResendAssignmentOtpCommand)request)
            .Returns(Task.CompletedTask);

        var result = await _controller.ResendOtp(
            assignmentId,
            new DriverResendOtpRequest("pickup"),
            currentUserService,
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        sentCommand.Should().NotBeNull();
        sentCommand!.AssignmentId.Should().Be(assignmentId);
        sentCommand.DriverUserId.Should().Be(userId);
        sentCommand.OtpType.Should().Be("pickup");
    }
}
