namespace Zadana.Application.Common.Interfaces;

public sealed record CurrentVendorScope(Guid VendorId, Guid? BranchId);

public interface ICurrentVendorService
{
    Task<Guid?> TryGetVendorIdAsync(CancellationToken cancellationToken = default);
    Task<Guid> GetRequiredVendorIdAsync(CancellationToken cancellationToken = default);
    Task<CurrentVendorScope?> TryGetVendorScopeAsync(CancellationToken cancellationToken = default);
    Task<CurrentVendorScope> GetRequiredVendorScopeAsync(CancellationToken cancellationToken = default);
}
