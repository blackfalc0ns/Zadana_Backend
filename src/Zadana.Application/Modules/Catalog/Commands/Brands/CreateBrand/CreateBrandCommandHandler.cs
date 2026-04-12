using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.CreateBrand;

public class CreateBrandCommandHandler : IRequestHandler<CreateBrandCommand, BrandDto>
{
    private readonly IApplicationDbContext _context;

    public CreateBrandCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BrandDto> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .FirstAsync(item => item.Id == request.CategoryId && item.ParentCategoryId != null, cancellationToken);

        var brand = new Brand(request.NameAr, request.NameEn, request.LogoUrl, request.CategoryId);

        _context.Brands.Add(brand);
        await _context.SaveChangesAsync(cancellationToken);

        return new BrandDto(
            brand.Id,
            brand.NameAr,
            brand.NameEn,
            brand.LogoUrl,
            brand.CategoryId,
            category.NameAr,
            category.NameEn,
            brand.IsActive,
            0,
            brand.CreatedAtUtc,
            brand.UpdatedAtUtc);
    }
}
