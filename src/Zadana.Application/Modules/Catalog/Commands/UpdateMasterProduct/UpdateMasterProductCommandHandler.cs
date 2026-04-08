using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.UpdateMasterProduct;

public class UpdateMasterProductCommandHandler : IRequestHandler<UpdateMasterProductCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateMasterProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateMasterProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.MasterProducts
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product == null)
            throw new NotFoundException("MasterProduct", request.Id);

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
            throw new NotFoundException("Category", request.CategoryId);

        if (request.BrandId.HasValue && !await _context.Brands.AnyAsync(b => b.Id == request.BrandId.Value, cancellationToken))
            throw new NotFoundException("Brand", request.BrandId.Value);

        product.UpdateDetails(
            nameAr: request.NameAr,
            nameEn: request.NameEn,
            slug: request.Slug,
            descriptionAr: request.DescriptionAr,
            descriptionEn: request.DescriptionEn,
            barcode: request.Barcode
        );

        product.ChangeCategory(request.CategoryId);
        product.ChangeBrand(request.BrandId);
        product.ChangeUnit(request.UnitId);

        if (request.Status.HasValue)
        {
            product.SetStatus(request.Status.Value);
        }

        // Update Images
        if (request.Images != null)
        {
            product.ClearImages();
            foreach (var img in request.Images)
            {
                product.AddImage(img.Url, img.AltText, img.DisplayOrder, img.IsPrimary);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
