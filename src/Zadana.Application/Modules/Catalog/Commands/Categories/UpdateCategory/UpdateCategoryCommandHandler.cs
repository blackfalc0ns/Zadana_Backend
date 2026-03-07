using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.Categories.UpdateCategory;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories.FindAsync(new object[] { request.Id }, cancellationToken);
        if (category == null)
            throw new NotFoundException(nameof(Category), request.Id);

        if (request.ParentCategoryId.HasValue)
        {
            var parentExists = await _context.Categories.FindAsync(new object[] { request.ParentCategoryId.Value }, cancellationToken);
            if (parentExists == null)
                throw new NotFoundException(nameof(Category), request.ParentCategoryId.Value);
        }

        category.Update(request.NameAr, request.NameEn, request.ImageUrl, request.ParentCategoryId, request.DisplayOrder);

        if (request.IsActive && !category.IsActive)
            category.Activate();
        else if (!request.IsActive && category.IsActive)
            category.Deactivate();

        await _context.SaveChangesAsync(cancellationToken);
    }
}
