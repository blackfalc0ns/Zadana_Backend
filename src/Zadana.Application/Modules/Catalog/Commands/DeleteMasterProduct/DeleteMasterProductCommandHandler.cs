using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.DeleteMasterProduct;

public class DeleteMasterProductCommandHandler : IRequestHandler<DeleteMasterProductCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public DeleteMasterProductCommandHandler(
        IApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task Handle(DeleteMasterProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.MasterProducts
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException("MasterProduct", request.Id);
        }

        _context.MasterProducts.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
