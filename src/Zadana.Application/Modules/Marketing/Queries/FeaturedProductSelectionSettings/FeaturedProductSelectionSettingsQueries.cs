using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.DTOs;

namespace Zadana.Application.Modules.Marketing.Queries.FeaturedProductSelectionSettings;

public record GetFeaturedProductSelectionSettingsQuery() : IRequest<FeaturedProductSelectionSettingsDto>;

public class GetFeaturedProductSelectionSettingsQueryHandler : IRequestHandler<GetFeaturedProductSelectionSettingsQuery, FeaturedProductSelectionSettingsDto>
{
    private readonly IApplicationDbContext _context;

    public GetFeaturedProductSelectionSettingsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<FeaturedProductSelectionSettingsDto> Handle(GetFeaturedProductSelectionSettingsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await _context.FeaturedProductSelectionSettings
                .AsNoTracking()
                .OrderBy(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            return entity is null
                ? MarketingDatabaseObjectFallbacks.CreateDefaultFeaturedProductSelectionSettings()
                : new FeaturedProductSelectionSettingsDto(
                    entity.SelectionMode.ToString(),
                    entity.TargetCount,
                    entity.MinSalesCount,
                    entity.MinStoreCount,
                    entity.RequireDiscount,
                    entity.ExcludeProductsAlreadyInSpecialOffers);
        }
        catch (Exception ex) when (MarketingDatabaseObjectFallbacks.IsMissingDatabaseObject(ex))
        {
            return MarketingDatabaseObjectFallbacks.CreateDefaultFeaturedProductSelectionSettings();
        }
    }
}
