using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Services;

public class RegistrationWorkflow : IRegistrationWorkflow
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RegistrationWorkflow(
        IIdentityAccountService identityAccountService,
        IRefreshTokenStore refreshTokenStore,
        IJwtTokenService jwtTokenService,
        IStringLocalizer<SharedResource> localizer)
    {
        _identityAccountService = identityAccountService;
        _refreshTokenStore = refreshTokenStore;
        _jwtTokenService = jwtTokenService;
        _localizer = localizer;
    }

    public async Task<IdentityAccountSnapshot> RegisterAccountAsync(
        CreateIdentityAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var createResult = await _identityAccountService.CreateAsync(request, cancellationToken);

        if (createResult.Status == IdentityCreateStatus.DuplicateEmailOrPhone)
        {
            throw new BusinessRuleException("USER_ALREADY_EXISTS", _localizer["USER_ALREADY_EXISTS"]);
        }

        if (createResult.Status != IdentityCreateStatus.Succeeded || createResult.Account == null)
        {
            var errors = string.Join(", ", createResult.Errors ?? []);
            throw new BusinessRuleException("CREATION_FAILED", $"{_localizer["CREATION_FAILED"]}: {errors}");
        }

        return createResult.Account;
    }

    public async Task<AuthResponseDto> BuildAuthResponseAsync(
        IdentityAccountSnapshot account,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _jwtTokenService.GenerateTokenPairAsync(account, cancellationToken);
        _refreshTokenStore.Add(new NewRefreshToken(account.Id, tokens.RefreshToken, DateTime.UtcNow.Add(RefreshTokenLifetime)));

        var userDto = new CurrentUserDto(
            account.Id,
            account.FullName,
            account.Email,
            account.PhoneNumber,
            account.Role.ToString());

        return new AuthResponseDto(tokens, userDto);
    }

    public async Task CompensateAccountCreationFailureAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var deleteResult = await _identityAccountService.DeleteAsync(userId, cancellationToken);
        if (!deleteResult.Succeeded)
        {
            var errors = string.Join(", ", deleteResult.Errors ?? []);
            throw new BusinessRuleException("IDENTITY_COMPENSATION_FAILED", $"{_localizer["IDENTITY_OPERATION_FAILED"]}: {errors}");
        }
    }
}
