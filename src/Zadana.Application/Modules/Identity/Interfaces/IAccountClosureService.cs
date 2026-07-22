namespace Zadana.Application.Modules.Identity.Interfaces;

public interface IAccountClosureService
{
    Task CloseCustomerAccountAsync(
        Guid userId,
        string password,
        string confirmation,
        string? reason,
        CancellationToken cancellationToken = default);

    Task CloseDriverAccountAsync(
        Guid userId,
        string password,
        string confirmation,
        string? reason,
        CancellationToken cancellationToken = default);

    Task CloseVendorAccountAsync(
        Guid userId,
        string password,
        string confirmation,
        string? reason,
        CancellationToken cancellationToken = default);
}
