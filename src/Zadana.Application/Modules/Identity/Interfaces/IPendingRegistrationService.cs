using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Interfaces;

/// <summary>
/// Stateless pending registration: signup data lives only in a signed token until OTP succeeds.
/// Nothing is written to AspNetUsers or any pending table before verification.
/// </summary>
public interface IPendingRegistrationService
{
    Task<PendingRegistrationStartResult> StartAsync(
        StartPendingRegistrationRequest request,
        CancellationToken cancellationToken = default);

    Task<PendingOtpDispatchResult> ResendOtpAsync(
        string registrationToken,
        UserRole? expectedRole = null,
        CancellationToken cancellationToken = default);

    Task<PendingCompletionResult> VerifyAndCreateAccountAsync(
        string registrationToken,
        string otpCode,
        CancellationToken cancellationToken = default);
}
