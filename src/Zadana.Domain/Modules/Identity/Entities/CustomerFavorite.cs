using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Identity.Entities;

public class CustomerFavorite : BaseEntity
{
    public Guid? UserId { get; private set; }
    public string? GuestId { get; private set; }
    public Guid MasterProductId { get; private set; }

    public User? User { get; private set; }
    public MasterProduct MasterProduct { get; private set; } = null!;

    private CustomerFavorite() { }

    public CustomerFavorite(Guid? userId, string? guestId, Guid masterProductId)
    {
        if (!userId.HasValue && string.IsNullOrWhiteSpace(guestId))
        {
            throw new InvalidOperationException("Favorite owner is required.");
        }

        UserId = userId;
        GuestId = string.IsNullOrWhiteSpace(guestId) ? null : guestId.Trim();
        MasterProductId = masterProductId;
    }
}
