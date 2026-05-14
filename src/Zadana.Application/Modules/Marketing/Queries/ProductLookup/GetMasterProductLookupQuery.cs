using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Marketing.DTOs;

namespace Zadana.Application.Modules.Marketing.Queries.ProductLookup;

public record GetMasterProductLookupQuery(string? Search) : IRequest<List<MasterProductLookupDto>>;

public class GetMasterProductLookupQueryHandler : IRequestHandler<GetMasterProductLookupQuery, List<MasterProductLookupDto>>
{
    private readonly IApplicationDbContext _context;
    public GetMasterProductLookupQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<MasterProductLookupDto>> Handle(GetMasterProductLookupQuery request, CancellationToken cancellationToken)
    {
        var query = _context.MasterProducts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(p =>
                p.NameAr.ToLower().Contains(search) ||
                p.NameEn.ToLower().Contains(search));
        }

        return await query
            .OrderBy(p => p.NameAr)
            .Take(50)
            .Select(p => new MasterProductLookupDto(p.Id, p.NameAr, p.NameEn))
            .ToListAsync(cancellationToken);
    }
}
