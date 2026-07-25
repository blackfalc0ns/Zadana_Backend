using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Application.Modules.Orders.Support;

public static class PlatformPickupSettingsSupport
{
    public static async Task<PlatformPickupSettings> LoadAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        return await context.PlatformPickupSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == PlatformPickupSettings.SingletonId, cancellationToken)
            ?? new PlatformPickupSettings();
    }

    public static TimeSpan ResolveOtpTtl(PlatformPickupSettings settings) =>
        TimeSpan.FromHours(Math.Max(1, settings.PickupNoShowTimeoutHours));

    public static TimeSpan ResolveNoShowTimeout(PlatformPickupSettings settings) =>
        TimeSpan.FromHours(Math.Max(1, settings.PickupNoShowTimeoutHours));
}
