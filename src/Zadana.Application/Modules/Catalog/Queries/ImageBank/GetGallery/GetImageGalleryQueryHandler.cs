using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Models;

namespace Zadana.Application.Modules.Catalog.Queries.ImageBank.GetGallery;

public class GetImageGalleryQueryHandler : IRequestHandler<GetImageGalleryQuery, PaginatedList<ImageBankDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetImageGalleryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<ImageBankDto>> Handle(GetImageGalleryQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ImageBanks.AsNoTracking();

        // 1. Filter by Current User
        if (_currentUserService.Role == "Vendor" && _currentUserService.UserId.HasValue)
        {
            query = query.Where(ib => ib.UploadedByVendorId == _currentUserService.UserId.Value);
        }
        else if (_currentUserService.Role != "SuperAdmin" && _currentUserService.Role != "Admin")
        {
            // If they are not Admin or Vendor, they shouldn't see anything here (or return empty)
            return new PaginatedList<ImageBankDto>([], 0, request.PageNumber, request.PageSize);
        }

        // 2. Apply Search Filter
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(ib => 
                (ib.AltText != null && ib.AltText.ToLower().Contains(searchTerm)) ||
                (ib.Tags != null && ib.Tags.ToLower().Contains(searchTerm))
            );
        }

        // 3. Paginate
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderByDescending(ib => ib.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ib => new ImageBankDto(
                ib.Id,
                ib.Url,
                ib.AltText,
                ib.Tags,
                ib.Status.ToString(),
                ib.RejectionReason
            ))
            .ToListAsync(cancellationToken);

        return new PaginatedList<ImageBankDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
