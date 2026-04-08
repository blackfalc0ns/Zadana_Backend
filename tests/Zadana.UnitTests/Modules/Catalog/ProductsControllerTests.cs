using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Zadana.Api.Modules.Catalog.Controllers;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Products.GetProductDetails;

namespace Zadana.UnitTests.Modules.Catalog;

public class ProductsControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly ProductsController _controller;

    public ProductsControllerTests()
    {
        _controller = new ProductsController();

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
    public async Task GetProduct_ReturnsOkResult()
    {
        var dto = new ProductDetailsDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Milk",
            "Store",
            10m,
            12m,
            "image.jpg",
            ["image.jpg"],
            4.5m,
            3,
            "17%",
            false,
            "Liter",
            true,
            "Description",
            [],
            []);

        _senderMock.Setup(x => x.Send(It.IsAny<GetProductDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.GetProduct(Guid.NewGuid(), CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }
}
