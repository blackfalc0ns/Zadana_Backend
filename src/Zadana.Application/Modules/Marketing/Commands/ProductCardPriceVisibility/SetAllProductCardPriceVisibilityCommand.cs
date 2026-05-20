using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.DTOs;

namespace Zadana.Application.Modules.Marketing.Commands.ProductCardPriceVisibility;

public record SetAllProductCardPriceVisibilityCommand(bool ShowPriceOnCard)
    : IRequest<ProductCardPriceVisibilitySettingDto>;

public class SetAllProductCardPriceVisibilityCommandHandler
    : IRequestHandler<SetAllProductCardPriceVisibilityCommand, ProductCardPriceVisibilitySettingDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public SetAllProductCardPriceVisibilityCommandHandler(
        IApplicationDbContext context,
        ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<ProductCardPriceVisibilitySettingDto> Handle(
        SetAllProductCardPriceVisibilityCommand request,
        CancellationToken cancellationToken)
    {
        var productsToUpdate = await _context.MasterProducts
            .Where(product => product.ShowPriceOnCard != request.ShowPriceOnCard)
            .ToListAsync(cancellationToken);

        foreach (var product in productsToUpdate)
        {
            product.SetCardPriceVisibility(request.ShowPriceOnCard);
        }

        if (productsToUpdate.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
        }

        var totalProducts = await _context.MasterProducts.CountAsync(cancellationToken);
        var visibleProducts = request.ShowPriceOnCard ? totalProducts : 0;
        var hiddenProducts = request.ShowPriceOnCard ? 0 : totalProducts;

        return new ProductCardPriceVisibilitySettingDto(
            request.ShowPriceOnCard,
            totalProducts,
            visibleProducts,
            hiddenProducts,
            false);
    }
}
