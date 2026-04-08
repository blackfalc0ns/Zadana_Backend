using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.Commands.HomeBanners;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Marketing.Queries.HomeBanners;

public record GetHomeBannersQuery() : IRequest<List<HomeBannerAdminDto>>;
public record GetHomeBannerByIdQuery(Guid Id) : IRequest<HomeBannerAdminDto>;

public class GetHomeBannersQueryHandler : IRequestHandler<GetHomeBannersQuery, List<HomeBannerAdminDto>>
{
    private readonly IApplicationDbContext _context;
    public GetHomeBannersQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<HomeBannerAdminDto>> Handle(GetHomeBannersQuery request, CancellationToken cancellationToken) =>
        await _context.HomeBanners
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => MarketingMappings.ToDto(x))
            .ToListAsync(cancellationToken);
}

public class GetHomeBannerByIdQueryHandler : IRequestHandler<GetHomeBannerByIdQuery, HomeBannerAdminDto>
{
    private readonly IApplicationDbContext _context;
    public GetHomeBannerByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<HomeBannerAdminDto> Handle(GetHomeBannerByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.HomeBanners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(HomeBanner), request.Id);

        return MarketingMappings.ToDto(entity);
    }
}
