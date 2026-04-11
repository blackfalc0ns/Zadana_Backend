using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Identity.Entities;

public class CustomerFavorite : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid MasterProductId { get; private set; }

    public User User { get; private set; } = null!;
    public MasterProduct MasterProduct { get; private set; } = null!;

    private CustomerFavorite() { }

    public CustomerFavorite(Guid userId, Guid masterProductId)
    {
        UserId = userId;
        MasterProductId = masterProductId;
    }
}
