using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IJwtRevocationStore _jwtRevocationStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ResetPasswordCommandHandler(
        IIdentityAccountService identityAccountService,
        IRefreshTokenStore refreshTokenStore,
        IJwtRevocationStore jwtRevocationStore,
        IUnitOfWork unitOfWork,
        IStringLocalizer<SharedResource> localizer)
    {
        _identityAccountService = identityAccountService;
        _refreshTokenStore = refreshTokenStore;
        _jwtRevocationStore = jwtRevocationStore;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            throw new UnauthorizedException(_localizer["InvalidResetAttempt"]);
        }

        var account = await _identityAccountService.FindByIdentifierAsync(request.Identifier, cancellationToken);

        var resetResult = await _identityAccountService.CompletePasswordResetAsync(
            request.Identifier,
            request.ResetToken,
            request.NewPassword,
            cancellationToken);

        if (resetResult.Status == PasswordResetStatus.UserNotFound)
        {
            throw new UnauthorizedException(_localizer["InvalidResetAttempt"]);
        }

        if (resetResult.Status == PasswordResetStatus.InvalidOrExpiredResetToken)
        {
            throw new BusinessRuleException("INVALID_RESET_TOKEN", _localizer["InvalidOrExpiredResetToken"]);
        }

        if (resetResult.Status == PasswordResetStatus.InvalidOrExpiredOtp)
        {
            throw new BusinessRuleException("INVALID_OTP", _localizer["InvalidOrExpiredOtp"]);
        }

        if (resetResult.Status == PasswordResetStatus.Failed)
        {
            var errors = string.Join(", ", resetResult.Errors ?? []);
            var message = string.IsNullOrWhiteSpace(errors)
                ? _localizer["PASSWORD_RESET_FAILED"].Value
                : errors;
            throw new BusinessRuleException("PASSWORD_RESET_FAILED", message);
        }

        if (account is not null)
        {
            await _refreshTokenStore.RevokeAllByUserAsync(account.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _jwtRevocationStore.RevokeAllForUserAsync(account.Id, DateTime.UtcNow, cancellationToken);
        }
    }
}
