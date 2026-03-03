using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (request.ParentId.HasValue)
        {
            var parentExists = _context.Categories.Any(c => c.Id == request.ParentId.Value);
            if (!parentExists)
            {
                throw new NotFoundException("ParentCategory", request.ParentId.Value);
            }
        }

        var category = new Category(
            nameAr: request.Name, // Default mapping as command lacks language splits
            nameEn: request.Name,
            parentCategoryId: request.ParentId,
            displayOrder: request.SortOrder
        );

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
