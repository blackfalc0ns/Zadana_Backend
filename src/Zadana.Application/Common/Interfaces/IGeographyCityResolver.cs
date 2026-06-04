using Zadana.Application.Modules.Geography;

namespace Zadana.Application.Common.Interfaces;

public interface IGeographyCityResolver
{
    ResolvedCity Resolve(string? rawCity);

    ResolvedCity ResolveLocation(string? city, string? region);

    Task RefreshCatalogAsync(CancellationToken cancellationToken = default);
}
