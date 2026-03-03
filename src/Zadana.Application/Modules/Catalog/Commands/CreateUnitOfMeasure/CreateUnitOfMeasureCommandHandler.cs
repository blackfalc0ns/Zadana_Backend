using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Catalog.Commands.CreateUnitOfMeasure;

public class CreateUnitOfMeasureCommandHandler : IRequestHandler<CreateUnitOfMeasureCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateUnitOfMeasureCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateUnitOfMeasureCommand request, CancellationToken cancellationToken)
    {
        var uom = new UnitOfMeasure(
            nameAr: request.Name,
            nameEn: request.Name
        );

        _context.UnitsOfMeasure.Add(uom);
        await _context.SaveChangesAsync(cancellationToken);

        return uom.Id;
    }
}
