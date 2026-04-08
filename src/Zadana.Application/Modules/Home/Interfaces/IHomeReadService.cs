using Zadana.Application.Modules.Home.DTOs;

namespace Zadana.Application.Modules.Home.Interfaces;

public interface IHomeReadService
{
    Task<HomeHeaderDto> GetHeaderAsync(CancellationToken cancellationToken = default);
    Task<HomeContentDto> GetContentAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HomeBannerDto>> GetBannersAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HomeCategoryDto>> GetCategoriesAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HomeProductCardDto>> GetSpecialOffersAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HomeProductCardDto>> GetRecommendedAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HomeProductCardDto>> GetBestSellingAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HomeBrandCardDto>> GetBrandsAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HomeProductCardDto>> GetFeaturedProductsAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HomeProductCardDto>> GetExploreMoreAsync(int take, CancellationToken cancellationToken = default);
}
