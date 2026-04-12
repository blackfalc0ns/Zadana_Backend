using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Api.Modules.Favorites.Controllers;
using Zadana.Api.Modules.Favorites.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Favorites.Commands;
using Zadana.Application.Modules.Favorites.DTOs;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Favorites;

public class FavoritesControllerTests
{
    private readonly Mock<IMediator> _senderMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock = new();
    private readonly FavoritesController _controller;

    public FavoritesControllerTests()
    {
        _currentUserServiceMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _currentUserServiceMock.SetupGet(x => x.IsAuthenticated).Returns(true);
        _localizerMock.Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key switch
            {
                "InvalidProductId" => "Invalid product id.",
                "UserNotAuthenticated" => "User is not authenticated.",
                _ => key
            }));
        _localizerMock.Setup(x => x["GuestFavoritesHeaderRequired", It.IsAny<object[]>()])
            .Returns((string key, object[] args) =>
                new LocalizedString(key, $"User is not authenticated. Send {args[0]} header for guest favorites access."));

        _controller = new FavoritesController(_senderMock.Object, _currentUserServiceMock.Object, _localizerMock.Object);

        var services = new ServiceCollection();
        services.AddSingleton(_senderMock.Object);
        var provider = services.BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                RequestServices = provider
            }
        };
    }

    [Fact]
    public async Task AddFavorite_ReturnsLocalizedBadRequest_WhenProductIdIsInvalid()
    {
        var act = async () => await _controller.AddFavorite(new AddFavoriteRequest("not-a-guid"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BadRequestException>();
        exception.Which.Message.Should().Be("Invalid product id.");
    }

    [Fact]
    public async Task GetFavorites_ThrowsLocalizedUnauthorized_WhenGuestHeaderIsMissing()
    {
        _currentUserServiceMock.SetupGet(x => x.UserId).Returns((Guid?)null);
        _currentUserServiceMock.SetupGet(x => x.IsAuthenticated).Returns(false);

        var act = async () => await _controller.GetFavorites(CancellationToken.None);

        var exception = await act.Should().ThrowAsync<UnauthorizedException>();
        exception.Which.Message.Should().Be("User is not authenticated. Send X-Device-Id header for guest favorites access.");
    }

    [Fact]
    public async Task AddFavorite_ReturnsOkResult()
    {
        var response = new AddFavoriteResponse(
            "Product added to favorites successfully.",
            new FavoriteItemDto(Guid.NewGuid(), "Milk", "Green Valley Market", 50m, null, null, null, null, null, true, "Liter", false),
            new FavoritesSummaryDto(1));

        _senderMock.Setup(x => x.Send(It.IsAny<AddFavoriteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.AddFavorite(new AddFavoriteRequest(Guid.NewGuid().ToString()), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(response);
    }
}
