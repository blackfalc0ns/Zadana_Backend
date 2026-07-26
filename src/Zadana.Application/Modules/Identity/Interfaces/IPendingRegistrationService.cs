using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Interfaces;

public interface IPendingRegistrationService
{
    Task<PendingRegistrationStartResult> StartAsync(
        StartPendingRegistrationRequest request,
        CancellationToken cancellationToken = default);

    Task<PendingOtpDispatchResult> ResendOtpAsync(
        string identifier,
        UserRole? expectedRole = null,
        CancellationToken cancellationToken = default);

    Task<PendingCompletionResult> VerifyAndCreateAccountAsync(
        string identifier,
        string otpCode,
        CancellationToken cancellationToken = default);

    Task<PendingRegistrationSnapshot?> FindByIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken = default);

    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}
