using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Interfaces;

public interface IRegistrationWorkflow
{
    Task<IdentityAccountSnapshot> RegisterAccountAsync(
        CreateIdentityAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponseDto> BuildAuthResponseAsync(
        IdentityAccountSnapshot account,
        CancellationToken cancellationToken = default);

    Task CompensateAccountCreationFailureAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
