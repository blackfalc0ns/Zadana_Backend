using Zadana.Application.Modules.Home.DTOs;

namespace Zadana.Application.Modules.Home.Interfaces;

public interface IHomeReadService
{
    Task<HomeHeaderDto> GetHeaderAsync(CancellationToken cancellationToken = default);
    Task<HomeContentDto> GetContentAsync(CancellationToken cancellationToken = default);
    Task<HomeListSectionDto<HomeBannerDto>> GetBannersAsync(int take, CancellationToken cancellationToken = default);
    Task<HomeListSectionDto<HomeCategoryDto>> GetCategoriesAsync(int take, CancellationToken cancellationToken = default);
    Task<HomeListSectionDto<HomeProductCardDto>> GetSpecialOffersAsync(int take, CancellationToken cancellationToken = default);
    Task<HomeListSectionDto<HomeProductCardDto>> GetRecommendedAsync(int take, CancellationToken cancellationToken = default);
    Task<HomeListSectionDto<HomeProductCardDto>> GetBestSellingAsync(int take, CancellationToken cancellationToken = default);
    Task<HomeListSectionDto<HomeBrandCardDto>> GetBrandsAsync(int take, CancellationToken cancellationToken = default);
    Task<HomeListSectionDto<HomeProductCardDto>> GetFeaturedProductsAsync(int take, CancellationToken cancellationToken = default);
    Task<HomeListSectionDto<HomeProductCardDto>> GetExploreMoreAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HomeDynamicSectionDto>> GetDynamicSectionsAsync(CancellationToken cancellationToken = default);
}
