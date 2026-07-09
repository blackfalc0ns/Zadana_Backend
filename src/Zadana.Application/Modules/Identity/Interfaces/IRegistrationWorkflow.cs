using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Interfaces;

public interface IRegistrationWorkflow
{
    Task<IdentityAccountSnapshot> RegisterAccountAsync(
        CreateIdentityAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<IdentityAccountSnapshot> SendRegistrationOtpAsync(
        IdentityAccountSnapshot account,
        CancellationToken cancellationToken = default);

    Task<RegistrationOtpDispatch> GenerateRegistrationOtpAsync(
        IdentityAccountSnapshot account,
        CancellationToken cancellationToken = default);

    Task DispatchRegistrationOtpEmailAsync(
        string emailAddress,
        string otpCode,
        CancellationToken cancellationToken = default);

    Task<AuthResponseDto> BuildAuthResponseAsync(
        IdentityAccountSnapshot account,
        DriverOperationalStatusDto? driverStatus = null,
        CancellationToken cancellationToken = default);

    Task CompensateAccountCreationFailureAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
