using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;

namespace Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetVendorRequests;

public class GetVendorProductRequestsQueryHandler : IRequestHandler<GetVendorProductRequestsQuery, PaginatedList<ProductRequestDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentVendorService _currentVendorService;

    public GetVendorProductRequestsQueryHandler(IApplicationDbContext context, ICurrentVendorService currentVendorService)
    {
        _context = context;
        _currentVendorService = currentVendorService;
    }

    public async Task<PaginatedList<ProductRequestDto>> Handle(GetVendorProductRequestsQuery request, CancellationToken cancellationToken)
    {
        var vendorId = await _currentVendorService.TryGetVendorIdAsync(cancellationToken);

        if (vendorId is null)
        {
            return new PaginatedList<ProductRequestDto>([], 0, request.PageNumber, request.PageSize);
        }

        var query = _context.ProductRequests
            .Include(pr => pr.Category)
            .AsNoTracking()
            .Where(pr => pr.VendorId == vendorId.Value);

        if (request.Status.HasValue)
        {
            query = query.Where(pr => pr.Status == request.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(pr => pr.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(pr => new ProductRequestDto(
                pr.Id,
                pr.SuggestedNameAr,
                pr.SuggestedNameEn,
                pr.SuggestedDescriptionAr,
                pr.SuggestedDescriptionEn,
                pr.SuggestedCategoryId,
                pr.Category.NameAr,
                pr.Category.NameEn,
                pr.ImageUrl,
                pr.Status.ToString(),
                pr.RejectionReason,
                pr.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return new PaginatedList<ProductRequestDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
