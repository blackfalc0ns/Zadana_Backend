using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;

public class CreateMasterProductCommandHandler : IRequestHandler<CreateMasterProductCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateMasterProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateMasterProductCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = _context.Categories.Any(c => c.Id == request.CategoryId);
        if (!categoryExists)
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        if (request.BrandId.HasValue && !_context.Brands.Any(b => b.Id == request.BrandId.Value))
        {
            throw new NotFoundException("Brand", request.BrandId.Value);
        }

        var masterProduct = new MasterProduct(
            nameAr: request.NameAr,
            nameEn: request.NameEn,
            slug: request.Slug,
            categoryId: request.CategoryId,
            brandId: request.BrandId,
            unitOfMeasureId: request.UnitId,
            descriptionAr: request.DescriptionAr,
            descriptionEn: request.DescriptionEn,
            barcode: request.Barcode
        );

        masterProduct.SetStatus(request.Status);

        if (request.Images != null)
        {
            foreach (var img in request.Images)
            {
                masterProduct.AddImage(img.Url, img.AltText, img.DisplayOrder, img.IsPrimary);
            }
        }

        _context.MasterProducts.Add(masterProduct);
        await _context.SaveChangesAsync(cancellationToken);

        return masterProduct.Id;
    }
}
