using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Api.Modules.Identity.Controllers;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.Commands.UpdateCurrentUserProfile;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Queries.GetCurrentUser;

namespace Zadana.UnitTests.Modules.Identity.Controllers;

public class CustomerAuthControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock = new();
    private readonly CustomerAuthController _controller;

    public CustomerAuthControllerTests()
    {
        _localizerMock.Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _controller = new CustomerAuthController(_localizerMock.Object);

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
    public async Task GetCurrentUser_ReturnsOkResult()
    {
        var dto = new CurrentUserDto(Guid.NewGuid(), "Test User", "test@example.com", "01000000000", "Customer");

        _senderMock.Setup(x => x.Send(It.IsAny<GetCurrentUserQuery>(), default))
            .ReturnsAsync(dto);

        var result = await _controller.GetCurrentUser();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task UpdateCurrentUser_ReturnsOkResult_WithUpdatedProfile()
    {
        var request = new UpdateProfileRequest("Updated User", "updated@example.com", "01111111111");
        var dto = new CurrentUserDto(Guid.NewGuid(), request.FullName, request.Email, request.Phone, "Customer");

        _senderMock.Setup(x => x.Send(It.IsAny<UpdateCurrentUserProfileCommand>(), default))
            .ReturnsAsync(dto);

        var result = await _controller.UpdateCurrentUser(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
        _senderMock.Verify(x => x.Send(
            It.Is<UpdateCurrentUserProfileCommand>(command =>
                command.FullName == request.FullName
                && command.Email == request.Email
                && command.Phone == request.Phone),
            default), Times.Once);
    }
}
