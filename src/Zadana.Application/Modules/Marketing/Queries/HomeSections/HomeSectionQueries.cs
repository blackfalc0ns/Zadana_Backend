using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing;
using Zadana.Application.Modules.Marketing.Commands.HomeSections;
using Zadana.Application.Modules.Marketing.DTOs;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Marketing.Queries.HomeSections;

public record GetHomeSectionsQuery() : IRequest<List<HomeSectionAdminDto>>;
public record GetHomeSectionByIdQuery(Guid Id) : IRequest<HomeSectionAdminDto>;

public class GetHomeSectionsQueryHandler : IRequestHandler<GetHomeSectionsQuery, List<HomeSectionAdminDto>>
{
    private readonly IApplicationDbContext _context;
    public GetHomeSectionsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<HomeSectionAdminDto>> Handle(GetHomeSectionsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var items = await _context.HomeSections
                .AsNoTracking()
                .OrderBy(x => x.DisplayOrder)
                .ThenByDescending(x => x.CreatedAtUtc)
                .Select(x => new
                {
                    Entity = x,
                    x.Category.NameAr,
                    x.Category.NameEn
                })
                .ToListAsync(cancellationToken);

            return items.Select(x => MarketingMappings.ToDto(x.Entity, x.NameAr, x.NameEn)).ToList();
        }
        catch (Exception ex) when (MarketingDatabaseObjectFallbacks.IsMissingDatabaseObject(ex))
        {
            return [];
        }
    }
}

public class GetHomeSectionByIdQueryHandler : IRequestHandler<GetHomeSectionByIdQuery, HomeSectionAdminDto>
{
    private readonly IApplicationDbContext _context;
    public GetHomeSectionByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<HomeSectionAdminDto> Handle(GetHomeSectionByIdQuery request, CancellationToken cancellationToken)
    {
        var exists = await _context.HomeSections.AsNoTracking().AnyAsync(x => x.Id == request.Id, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(nameof(HomeSection), request.Id);
        }

        return await _context.ProjectHomeSectionAsync(request.Id, cancellationToken);
    }
}
