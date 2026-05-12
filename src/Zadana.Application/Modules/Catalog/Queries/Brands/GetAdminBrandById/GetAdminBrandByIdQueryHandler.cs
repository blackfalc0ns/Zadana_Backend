using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Queries.Brands.GetAdminBrandById;

public class GetAdminBrandByIdQueryHandler : IRequestHandler<GetAdminBrandByIdQuery, BrandDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminBrandByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BrandDto> Handle(GetAdminBrandByIdQuery request, CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .AsNoTracking()
            .Where(item => item.Id == request.BrandId)
            .Select(item => new BrandDto(
                item.Id,
                item.NameAr,
                item.NameEn,
                item.LogoUrl,
                item.CoverImageUrl,
                item.CategoryId,
                item.Category != null ? item.Category.NameAr : null,
                item.Category != null ? item.Category.NameEn : null,
                item.IsActive,
                item.MasterProducts.Count(),
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Brand), request.BrandId);

        return brand;
    }
}
