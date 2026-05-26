using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Zadana.Api.Modules.Social.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Social.Commands;
using Zadana.Application.Modules.Social.Queries;

namespace Zadana.UnitTests.Modules.Social;

public class NotificationDevicesControllerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ISender> _senderMock = new();

    [Fact]
    public void GetPreferences_ShouldExposeGetPreferencesRoute()
    {
        var method = typeof(NotificationDevicesController).GetMethod(nameof(NotificationDevicesController.GetPreferences));

        var attribute = method!
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<HttpGetAttribute>()
            .Subject;

        attribute.Template.Should().Be("preferences");
    }

    [Fact]
    public async Task GetPreferences_ShouldReturnCurrentDevicePreferences()
    {
        const string deviceId = "9825eeac-6851-4755-9826-7a72562e9230";
        const string deviceToken = "fcm-token";
        var dto = new NotificationDeviceDto(
            Guid.NewGuid(),
            deviceToken,
            "fcm",
            deviceId,
            "Android",
            "1.0.0",
            "ar",
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            "chime",
            true,
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow);

        _currentUserServiceMock.SetupGet(service => service.UserId).Returns(_userId);
        _senderMock
            .Setup(sender => sender.Send(
                It.Is<GetNotificationDevicePreferencesQuery>(query =>
                    query.UserId == _userId &&
                    query.DeviceId == deviceId &&
                    query.DeviceToken == deviceToken),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = CreateController();

        var result = await controller.GetPreferences(deviceId, deviceToken, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<NotificationDeviceResponse>().Subject;
        response.DeviceId.Should().Be(deviceId);
        response.DeviceToken.Should().Be(deviceToken);
        response.NotificationsEnabled.Should().BeTrue();
        response.NotificationSound.Should().Be("chime");
    }

    private NotificationDevicesController CreateController()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_senderMock.Object);
        var provider = services.BuildServiceProvider();

        return new NotificationDevicesController(_currentUserServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    RequestServices = provider
                }
            }
        };
    }
}
