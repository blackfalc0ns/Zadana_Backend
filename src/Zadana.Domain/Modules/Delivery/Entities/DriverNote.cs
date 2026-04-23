using Zadana.Domain.Modules.Identity.Entities;
using Zadana.SharedKernel.Primitives;

namespace Zadana.Domain.Modules.Delivery.Entities;

public class DriverNote : BaseEntity
{
    public Guid DriverId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Message { get; private set; } = null!;

    // Navigation
    public Driver Driver { get; private set; } = null!;
    public User Author { get; private set; } = null!;

    private DriverNote() { }

    public DriverNote(Guid driverId, Guid authorUserId, string message)
    {
        DriverId = driverId;
        AuthorUserId = authorUserId;
        Message = message.Trim();
    }
}
