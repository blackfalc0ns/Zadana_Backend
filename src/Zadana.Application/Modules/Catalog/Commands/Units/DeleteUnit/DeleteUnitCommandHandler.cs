using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Caching;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Catalog.Commands.Units.DeleteUnit;

public class DeleteUnitCommandHandler : IRequestHandler<DeleteUnitCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheInvalidator _cacheInvalidator;

    public DeleteUnitCommandHandler(IApplicationDbContext context, ICacheInvalidator cacheInvalidator)
    {
        _context = context;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task Handle(DeleteUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await _context.UnitsOfMeasure
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (unit is null)
        {
            throw new NotFoundException("UnitOfMeasure", request.Id);
        }

        // Guard: prevent deletion if products reference this unit
        var hasProducts = await _context.MasterProducts
            .AnyAsync(p =>
                p.UnitOfMeasureId == request.Id ||
                p.PackageTypeId == request.Id ||
                p.MeasurementUnitId == request.Id,
                cancellationToken);

        if (hasProducts)
        {
            throw new BusinessRuleException(
                "UNIT_HAS_PRODUCTS",
                "ما تقدر تحذف الوحدة لأنها مرتبطة بمنتجات.|Cannot delete this unit because it is linked to products.");
        }

        _context.UnitsOfMeasure.Remove(unit);
        await _context.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.RemoveByTagsAsync(CacheInvalidationProfiles.CatalogReadModels, cancellationToken);
    }
}
