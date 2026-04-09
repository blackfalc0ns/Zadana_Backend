using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Support;

public sealed record CartActor(Guid? UserId, string? GuestId)
{
    public bool IsAuthenticated => UserId.HasValue;

    public static CartActor Create(Guid? userId, string? guestId)
    {
        var normalizedGuestId = string.IsNullOrWhiteSpace(guestId) ? null : guestId.Trim();
        if (!userId.HasValue && normalizedGuestId is null)
        {
            throw new UnauthorizedException("Cart owner is required.");
        }

        return new CartActor(userId, normalizedGuestId);
    }
}
