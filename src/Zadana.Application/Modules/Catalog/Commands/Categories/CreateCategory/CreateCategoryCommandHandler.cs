using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.DTOs;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.Categories.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly IApplicationDbContext _context;

    public CreateCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (request.ParentCategoryId.HasValue)
        {
            var parentExists = await _context.Categories.FindAsync(new object[] { request.ParentCategoryId.Value }, cancellationToken);
            if (parentExists == null)
                throw new NotFoundException(nameof(Category), request.ParentCategoryId.Value);
        }

        var category = new Category(
            request.NameAr,
            request.NameEn,
            request.ImageUrl,
            request.ParentCategoryId,
            request.DisplayOrder);

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return new CategoryDto(
            category.Id,
            category.NameAr,
            category.NameEn,
            category.ImageUrl,
            category.ParentCategoryId,
            category.DisplayOrder,
            category.IsActive,
            CreatedAtUtc: category.CreatedAtUtc,
            UpdatedAtUtc: category.UpdatedAtUtc,
            BrandsCount: 0);
    }
}
