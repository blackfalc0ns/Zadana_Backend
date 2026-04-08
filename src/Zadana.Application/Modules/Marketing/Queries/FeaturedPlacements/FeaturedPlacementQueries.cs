using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.Commands.FeaturedPlacements;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Marketing.Queries.FeaturedPlacements;

public record GetFeaturedProductPlacementsQuery() : IRequest<List<FeaturedProductPlacementDto>>;
public record GetFeaturedProductPlacementByIdQuery(Guid Id) : IRequest<FeaturedProductPlacementDto>;

public class GetFeaturedProductPlacementsQueryHandler : IRequestHandler<GetFeaturedProductPlacementsQuery, List<FeaturedProductPlacementDto>>
{
    private readonly IApplicationDbContext _context;
    public GetFeaturedProductPlacementsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<FeaturedProductPlacementDto>> Handle(GetFeaturedProductPlacementsQuery request, CancellationToken cancellationToken)
    {
        var items = await _context.FeaturedProductPlacements
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                Entity = x,
                DisplayNameAr = x.PlacementType == FeaturedPlacementType.VendorProduct
                    ? (!string.IsNullOrWhiteSpace(x.VendorProduct!.CustomNameAr) ? x.VendorProduct.CustomNameAr : x.VendorProduct.MasterProduct.NameAr)
                    : x.MasterProduct!.NameAr,
                DisplayNameEn = x.PlacementType == FeaturedPlacementType.VendorProduct
                    ? (!string.IsNullOrWhiteSpace(x.VendorProduct!.CustomNameEn) ? x.VendorProduct.CustomNameEn : x.VendorProduct.MasterProduct.NameEn)
                    : x.MasterProduct!.NameEn
            })
            .ToListAsync(cancellationToken);

        return items.Select(x => MarketingMappings.ToDto(x.Entity, x.DisplayNameAr, x.DisplayNameEn)).ToList();
    }
}

public class GetFeaturedProductPlacementByIdQueryHandler : IRequestHandler<GetFeaturedProductPlacementByIdQuery, FeaturedProductPlacementDto>
{
    private readonly IApplicationDbContext _context;
    public GetFeaturedProductPlacementByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<FeaturedProductPlacementDto> Handle(GetFeaturedProductPlacementByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.FeaturedProductPlacements.AsNoTracking().AnyAsync(x => x.Id == request.Id, cancellationToken);
        if (!entity)
            throw new NotFoundException(nameof(FeaturedProductPlacement), request.Id);

        return await _context.ProjectPlacementAsync(request.Id, cancellationToken);
    }
}
