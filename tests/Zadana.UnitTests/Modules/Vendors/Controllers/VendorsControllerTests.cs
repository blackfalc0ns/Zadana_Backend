using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Api.Modules.Vendors.Controllers;
using Zadana.Api.Modules.Vendors.Requests;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Vendors.Commands.RegisterVendor;
using Zadana.Application.Modules.Vendors.Commands.UpdateVendorProfile;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Queries.GetVendorProfile;

namespace Zadana.UnitTests.Modules.Vendors.Controllers;

public class VendorsControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock = new();
    private readonly VendorsController _controller;

    public VendorsControllerTests()
    {
        _localizerMock.Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _controller = new VendorsController(_localizerMock.Object);

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
    public async Task RegisterVendor_ReturnsOkResult()
    {
        var request = new RegisterVendorRequest(
            "John Doe",
            "john@test.com",
            "1234567890",
            "password",
            "Business Ar",
            "Business En",
            "Retail",
            "CR123",
            null,
            "contact@test.com",
            "0987654321",
            null,
            null,
            "John Doe",
            "john@test.com",
            "1234567890",
            null,
            null,
            "Cairo",
            "Nasr City",
            "National Address 1",
            null,
            null,
            "Bank Misr",
            "John Doe",
            "SA0000000000000000000000",
            null,
            null,
            null,
            null,
            "Branch 1",
            "Address 1",
            0m,
            0m,
            "1111111111",
            5m);

        var authResponse = new AuthResponseDto(
            new TokenPairDto("access_token", "refresh_token"),
            new CurrentUserDto(Guid.NewGuid(), "John Doe", "john@test.com", "1234567890", "Vendor"));

        _senderMock.Setup(x => x.Send(It.IsAny<RegisterVendorCommand>(), default))
            .ReturnsAsync(authResponse);

        var result = await _controller.RegisterVendor(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetProfile_ReturnsOkResult()
    {
        var dto = new VendorWorkspaceDto(
            Guid.NewGuid(),
            "Ar",
            "En",
            "Type",
            "CR",
            null,
            null,
            null,
            "test@test.com",
            "123",
            null,
            null,
            null,
            null,
            null,
            "Owner",
            "owner@test.com",
            "123",
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
            DateTime.UtcNow,
            DateTime.UtcNow,
            1,
            1,
            null,
            []);

        _senderMock.Setup(x => x.Send(It.IsAny<GetVendorProfileQuery>(), default))
            .ReturnsAsync(dto);

        var result = await _controller.GetProfile();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsOkResult_WithLocalizedMessage()
    {
        var request = new UpdateVendorProfileRequest("Ar", "En", "Type", "test@test.com", "123", null);
        var dto = new VendorProfileDto(Guid.NewGuid(), "Ar", "En", "Type", "CR", null, "test@test.com", "123", null, "Active", null, null, DateTime.UtcNow);

        _senderMock.Setup(x => x.Send(It.IsAny<UpdateVendorProfileCommand>(), default))
            .ReturnsAsync(dto);

        var result = await _controller.UpdateProfile(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }
}
