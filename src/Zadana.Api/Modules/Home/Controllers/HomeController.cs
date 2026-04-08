using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zadana.Api.Controllers;
using Zadana.Application.Modules.Home.DTOs;
using Zadana.Application.Modules.Home.Interfaces;

namespace Zadana.Api.Modules.Home.Controllers;

[Route("api/home")]
[AllowAnonymous]
[Tags("Customer App API")]
public class HomeController : ApiControllerBase
{
    private readonly IHomeReadService _homeReadService;

    public HomeController(IHomeReadService homeReadService)
    {
        _homeReadService = homeReadService;
    }

    [HttpGet]
    public async Task<ActionResult<HomeHeaderDto>> GetHome(CancellationToken cancellationToken)
    {
        var result = await _homeReadService.GetHeaderAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("content")]
    public async Task<ActionResult<HomeContentDto>> GetHomeContent(CancellationToken cancellationToken)
    {
        var result = await _homeReadService.GetContentAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("banners")]
    public async Task<ActionResult<IReadOnlyList<HomeBannerDto>>> GetBanners([FromQuery] int take = 0, CancellationToken cancellationToken = default)
    {
        var result = await _homeReadService.GetBannersAsync(take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<HomeCategoryDto>>> GetCategories([FromQuery] int take = 0, CancellationToken cancellationToken = default)
    {
        var result = await _homeReadService.GetCategoriesAsync(take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("special-offers")]
    public async Task<ActionResult<IReadOnlyList<HomeProductCardDto>>> GetSpecialOffers([FromQuery] int take = 0, CancellationToken cancellationToken = default)
    {
        var result = await _homeReadService.GetSpecialOffersAsync(take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("recommended")]
    public async Task<ActionResult<IReadOnlyList<HomeProductCardDto>>> GetRecommended([FromQuery] int take = 0, CancellationToken cancellationToken = default)
    {
        var result = await _homeReadService.GetRecommendedAsync(take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("best-selling")]
    public async Task<ActionResult<IReadOnlyList<HomeProductCardDto>>> GetBestSelling([FromQuery] int take = 0, CancellationToken cancellationToken = default)
    {
        var result = await _homeReadService.GetBestSellingAsync(take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("brands")]
    public async Task<ActionResult<IReadOnlyList<HomeBrandCardDto>>> GetBrands([FromQuery] int take = 0, CancellationToken cancellationToken = default)
    {
        var result = await _homeReadService.GetBrandsAsync(take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("featured-products")]
    public async Task<ActionResult<IReadOnlyList<HomeProductCardDto>>> GetFeaturedProducts([FromQuery] int take = 0, CancellationToken cancellationToken = default)
    {
        var result = await _homeReadService.GetFeaturedProductsAsync(take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("explore-more")]
    public async Task<ActionResult<IReadOnlyList<HomeProductCardDto>>> GetExploreMore([FromQuery] int take = 0, CancellationToken cancellationToken = default)
    {
        var result = await _homeReadService.GetExploreMoreAsync(take, cancellationToken);
        return Ok(result);
    }
}

