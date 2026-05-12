using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Catalog.Entities;

public class BrandCategory : BaseEntity
{
    public Guid BrandId { get; private set; }
    public Guid CategoryId { get; private set; }

    public Brand Brand { get; private set; } = null!;
    public Category Category { get; private set; } = null!;

    private BrandCategory() { }

    public BrandCategory(Guid brandId, Guid categoryId)
    {
        BrandId = brandId;
        CategoryId = categoryId;
    }
}
