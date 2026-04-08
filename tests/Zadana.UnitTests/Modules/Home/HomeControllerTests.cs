using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Zadana.Api.Modules.Home.Controllers;
using Zadana.Application.Modules.Home.DTOs;
using Zadana.Application.Modules.Home.Interfaces;

namespace Zadana.UnitTests.Modules.Home;

public class HomeControllerTests
{
    private readonly Mock<IHomeReadService> _homeReadService = new();
    private readonly HomeController _controller;

    public HomeControllerTests()
    {
        _controller = new HomeController(_homeReadService.Object);
    }

    [Fact]
    public async Task GetHome_ReturnsOkResultWithHeader()
    {
        var dto = new HomeHeaderDto("Home", "Maadi, Cairo", "Street 2", 3);
        _homeReadService.Setup(x => x.GetHeaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await _controller.GetHome(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task GetSpecialOffers_ReturnsOkResultWithItems()
    {
        HomeListSectionDto<HomeProductCardDto> section = new(
            "special_offers",
            "Special Offers",
            true,
            null,
            1,
            [
                new HomeProductCardDto(Guid.NewGuid(), "Milk", "Store", 10m, 12m, "/milk.jpg", 4.5m, 5, "17%", false, false, "Liter", true)
            ]);

        _homeReadService.Setup(x => x.GetSpecialOffersAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(section);

        var result = await _controller.GetSpecialOffers(5, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(section);
    }
}
