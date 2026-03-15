namespace Zadana.Application.Common.Interfaces;

public interface ICurrentVendorService
{
    Task<Guid?> TryGetVendorIdAsync(CancellationToken cancellationToken = default);
    Task<Guid> GetRequiredVendorIdAsync(CancellationToken cancellationToken = default);
}
