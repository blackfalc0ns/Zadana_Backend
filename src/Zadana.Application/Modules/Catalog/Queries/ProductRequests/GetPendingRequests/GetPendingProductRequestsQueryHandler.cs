using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;
using Zadana.Domain.Modules.Catalog.Enums;

namespace Zadana.Application.Modules.Catalog.Queries.ProductRequests.GetPendingRequests;

public class GetPendingProductRequestsQueryHandler : IRequestHandler<GetPendingProductRequestsQuery, PaginatedList<AdminProductRequestDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPendingProductRequestsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<AdminProductRequestDto>> Handle(GetPendingProductRequestsQuery request, CancellationToken cancellationToken)
    {
        // Only Admin or SuperAdmin
        if (_currentUserService.Role != "Admin" && _currentUserService.Role != "SuperAdmin")
        {
            throw new UnauthorizedAccessException("غير مصرح لك باستعراض هذه الطلبات | You are not authorized to view these requests.");
        }

        var query = _context.ProductRequests
            .Include(pr => pr.Category)
            .Include(pr => pr.Vendor)
            .AsNoTracking()
            .Where(pr => pr.Status == ApprovalStatus.Pending);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(pr => pr.CreatedAtUtc) // Oldest first for fair processing
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(pr => new AdminProductRequestDto(
                pr.Id,
                pr.VendorId,
                pr.Vendor.BusinessNameAr,
                pr.SuggestedNameAr,
                pr.SuggestedNameEn,
                pr.SuggestedDescriptionAr,
                pr.SuggestedDescriptionEn,
                pr.SuggestedCategoryId,
                pr.Category.NameAr,
                pr.Category.NameEn,
                pr.ImageUrl,
                pr.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return new PaginatedList<AdminProductRequestDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
