using Zadana.Application.Modules.Delivery.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
    private readonly IAccessControlService _accessControlService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IOtpService _otpService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RegistrationWorkflow> _logger;

    public RegistrationWorkflow(
        IIdentityAccountService identityAccountService,
        IAccessControlService accessControlService,
        IRefreshTokenStore refreshTokenStore,
        IJwtTokenService jwtTokenService,
        IStringLocalizer<SharedResource> localizer,
        IOtpService otpService,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RegistrationWorkflow> logger)
    {
        _identityAccountService = identityAccountService;
        _accessControlService = accessControlService;
        _refreshTokenStore = refreshTokenStore;
        _jwtTokenService = jwtTokenService;
        _localizer = localizer;
        _otpService = otpService;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
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

    public async Task<IdentityAccountSnapshot> SendRegistrationOtpAsync(
        IdentityAccountSnapshot account,
        CancellationToken cancellationToken = default)
    {
        var otpResult = await GenerateRegistrationOtpInternalAsync(account, cancellationToken);
        DispatchRegistrationOtpEmail(otpResult.Account!.Email!, otpResult.OtpCode!);
        return otpResult.Account;
    }

    public Task<RegistrationOtpDispatch> GenerateRegistrationOtpAsync(
        IdentityAccountSnapshot account,
        CancellationToken cancellationToken = default) =>
        GenerateRegistrationOtpInternalAsync(account, cancellationToken);

    public void DispatchRegistrationOtpEmail(string emailAddress, string otpCode)
    {
        if (string.IsNullOrWhiteSpace(emailAddress) || string.IsNullOrWhiteSpace(otpCode))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var otpService = scope.ServiceProvider.GetRequiredService<IOtpService>();
                await otpService.SendOtpEmailAsync(emailAddress.Trim(), otpCode, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Background registration OTP email failed for {Email}. User can resend OTP.",
                    emailAddress);
            }
        });
    }

    public async Task<AuthResponseDto> BuildAuthResponseAsync(
        IdentityAccountSnapshot account,
        DriverOperationalStatusDto? driverStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (!account.EmailConfirmed)
        {
            var pendingUserDto = new CurrentUserDto(
                account.Id,
                account.FullName,
                account.Email,
                account.PhoneNumber,
                account.Role.ToString(),
                account.MustChangePassword,
                Access: await _accessControlService.GetEffectiveAccessAsync(account.Id, cancellationToken),
                ProfilePhotoUrl: account.ProfilePhotoUrl);

            return new AuthResponseDto(
                Tokens: null,
                User: pendingUserDto,
                IsVerified: false,
                Message: _localizer["AccountEmailNotVerified"],
                DriverStatus: driverStatus);
        }

        var tokens = await _jwtTokenService.GenerateTokenPairAsync(account, cancellationToken);
        _refreshTokenStore.Add(new NewRefreshToken(account.Id, tokens.RefreshToken, DateTime.UtcNow.Add(RefreshTokenLifetime)));

        var userDto = new CurrentUserDto(
            account.Id,
            account.FullName,
            account.Email,
            account.PhoneNumber,
            account.Role.ToString(),
            account.MustChangePassword,
            Access: await _accessControlService.GetEffectiveAccessAsync(account.Id, cancellationToken),
            ProfilePhotoUrl: account.ProfilePhotoUrl);

        var isVerified = AuthResponseVerificationResolver.Resolve(account, driverStatus);

        return new AuthResponseDto(tokens, userDto, IsVerified: isVerified, DriverStatus: driverStatus);
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

    private async Task<RegistrationOtpDispatch> GenerateRegistrationOtpInternalAsync(
        IdentityAccountSnapshot account,
        CancellationToken cancellationToken)
    {
        var otpResult = await _identityAccountService.GenerateRegistrationOtpAsync(account.Id, cancellationToken);

        if (otpResult.Status == OtpDispatchStatus.UserNotFound)
        {
            throw new BusinessRuleException("USER_NOT_FOUND", _localizer["USER_NOT_FOUND", account.Email ?? account.Id.ToString()]);
        }

        if (otpResult.Status == OtpDispatchStatus.Failed)
        {
            var errors = string.Join(", ", otpResult.Errors ?? []);
            throw new BusinessRuleException("IDENTITY_OPERATION_FAILED", $"{_localizer["IDENTITY_OPERATION_FAILED"]}: {errors}");
        }

        if (otpResult.Status != OtpDispatchStatus.Succeeded ||
            otpResult.Account == null ||
            string.IsNullOrWhiteSpace(otpResult.Account.Email) ||
            string.IsNullOrWhiteSpace(otpResult.OtpCode))
        {
            throw new BusinessRuleException("OTP_GENERATION_FAILED", _localizer["IDENTITY_OPERATION_FAILED"]);
        }

        return new RegistrationOtpDispatch(otpResult.Account, otpResult.OtpCode);
    }
}
