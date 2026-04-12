using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.Brands.UpdateBrand;

public class UpdateBrandCommandHandler : IRequestHandler<UpdateBrandCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateBrandCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
    {
        var brand = await _context.Brands.FindAsync(new object[] { request.Id }, cancellationToken);
        if (brand == null)
            throw new NotFoundException(nameof(Brand), request.Id);

        var category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.CategoryId && item.ParentCategoryId != null, cancellationToken);

        if (category == null)
            throw new NotFoundException(nameof(Category), request.CategoryId);

        brand.Update(request.NameAr, request.NameEn, request.LogoUrl, request.CategoryId);

        if (request.IsActive && !brand.IsActive)
            brand.Activate();
        else if (!request.IsActive && brand.IsActive)
            brand.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);
    }
}
