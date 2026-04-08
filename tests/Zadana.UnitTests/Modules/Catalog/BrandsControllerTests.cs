using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Zadana.Api.Modules.Catalog.Controllers;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandById;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandFilters;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandProducts;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetCustomerBrands;

namespace Zadana.UnitTests.Modules.Catalog;

public class BrandsControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly BrandsController _controller;

    public BrandsControllerTests()
    {
        _controller = new BrandsController();

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
    public async Task GetBrands_ReturnsOkResult()
    {
        List<BrandCustomerDto> dto =
        [
            new BrandCustomerDto(Guid.NewGuid(), "Almarai", "almarai.png", 12)
        ];

        _senderMock.Setup(x => x.Send(It.IsAny<GetCustomerBrandsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.GetBrands(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task GetProducts_BindsQueryAndReturnsOkResult()
    {
        var dto = new BrandProductsDto(
            new BrandProductsAppliedFiltersDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 20m, "newest"),
            1,
            1,
            20,
            []);

        _senderMock.Setup(x => x.Send(It.IsAny<GetBrandProductsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.GetProducts(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 20m, "newest", 1, 20, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dto);
    }
}
