using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.DTOs;

namespace Zadana.Application.Modules.Marketing.Queries.ProductCardPriceVisibility;

public record GetProductCardPriceVisibilityQuery() : IRequest<ProductCardPriceVisibilitySettingDto>;

public class GetProductCardPriceVisibilityQueryHandler
    : IRequestHandler<GetProductCardPriceVisibilityQuery, ProductCardPriceVisibilitySettingDto>
{
    private readonly IApplicationDbContext _context;

    public GetProductCardPriceVisibilityQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductCardPriceVisibilitySettingDto> Handle(
        GetProductCardPriceVisibilityQuery request,
        CancellationToken cancellationToken)
    {
        var counts = await _context.MasterProducts
            .AsNoTracking()
            .GroupBy(product => product.ShowPriceOnCard)
            .Select(group => new { ShowPriceOnCard = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var visibleProducts = counts.FirstOrDefault(item => item.ShowPriceOnCard)?.Count ?? 0;
        var hiddenProducts = counts.FirstOrDefault(item => !item.ShowPriceOnCard)?.Count ?? 0;
        var totalProducts = visibleProducts + hiddenProducts;
        var isMixed = visibleProducts > 0 && hiddenProducts > 0;
        var showPriceOnCard = hiddenProducts == 0;

        return new ProductCardPriceVisibilitySettingDto(
            showPriceOnCard,
            totalProducts,
            visibleProducts,
            hiddenProducts,
            isMixed);
    }
}
