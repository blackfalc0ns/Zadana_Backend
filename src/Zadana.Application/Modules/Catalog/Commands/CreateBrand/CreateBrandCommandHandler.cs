using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.Application.Modules.Catalog.Commands.CreateBrand;

public class CreateBrandCommandHandler : IRequestHandler<CreateBrandCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateBrandCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
    {
        var brand = new Brand(
            name: request.Name,
            logoUrl: request.LogoUrl
        );

        _context.Brands.Add(brand);
        await _context.SaveChangesAsync(cancellationToken);

        return brand.Id;
    }
}
