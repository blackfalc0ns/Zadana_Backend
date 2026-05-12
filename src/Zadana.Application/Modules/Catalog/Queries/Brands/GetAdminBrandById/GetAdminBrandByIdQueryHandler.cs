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
            .Include(item => item.Category)
            .Include(item => item.BrandCategories)
                .ThenInclude(link => link.Category)
            .Include(item => item.MasterProducts)
            .Where(item => item.Id == request.BrandId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Brand), request.BrandId);

        return MapBrand(brand);
    }

    private static BrandDto MapBrand(Brand brand)
    {
        var categories = brand.BrandCategories
            .Where(link => link.Category is not null)
            .OrderBy(link => link.Category.NameEn)
            .Select(link => new BrandCategoryLinkDto(link.CategoryId, link.Category.NameAr, link.Category.NameEn))
            .ToList();

        if (categories.Count == 0 && brand.CategoryId.HasValue)
        {
            categories.Add(new BrandCategoryLinkDto(
                brand.CategoryId.Value,
                brand.Category?.NameAr,
                brand.Category?.NameEn));
        }

        return new BrandDto(
            brand.Id,
            brand.NameAr,
            brand.NameEn,
            brand.LogoUrl,
            brand.CoverImageUrl,
            brand.CategoryId,
            categories.Select(item => item.CategoryId).ToList(),
            categories,
            brand.Category?.NameAr,
            brand.Category?.NameEn,
            brand.IsActive,
            brand.MasterProducts.Count,
            brand.CreatedAtUtc,
            brand.UpdatedAtUtc);
    }
}
