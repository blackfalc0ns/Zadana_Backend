using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Infrastructure.Modules.Identity.Services;

public sealed class PendingRegistrationService : IPendingRegistrationService
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly UserManager<User> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public PendingRegistrationService(
        IApplicationDbContext context,
        IIdentityAccountService identityAccountService,
        UserManager<User> userManager,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _identityAccountService = identityAccountService;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<PendingRegistrationStartResult> StartAsync(
        StartPendingRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = request.PhoneNumber.Trim();

        if (await _identityAccountService.ExistsByEmailOrPhoneAsync(email, phone, cancellationToken))
        {
            return new PendingRegistrationStartResult(PendingRegistrationStartStatus.DuplicateEmailOrPhone);
        }

        var existingByEmail = await _context.PendingRegistrations
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        var existingByPhone = await _context.PendingRegistrations
            .FirstOrDefaultAsync(x => x.PhoneNumber == phone, cancellationToken);

        if (existingByEmail is not null &&
            existingByPhone is not null &&
            existingByEmail.Id != existingByPhone.Id)
        {
            return new PendingRegistrationStartResult(PendingRegistrationStartStatus.DuplicateEmailOrPhone);
        }

        if (existingByPhone is not null &&
            existingByEmail is null &&
            !string.Equals(existingByPhone.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            return new PendingRegistrationStartResult(PendingRegistrationStartStatus.DuplicateEmailOrPhone);
        }

        if (existingByEmail is not null &&
            !string.Equals(existingByEmail.PhoneNumber, phone, StringComparison.Ordinal))
        {
            return new PendingRegistrationStartResult(PendingRegistrationStartStatus.DuplicateEmailOrPhone);
        }

        var passwordHash = HashPassword(request.Password);
        PendingRegistration pending;
        var existing = existingByEmail ?? existingByPhone;
        if (existing is not null)
        {
            if (existing.Role != request.Role)
            {
                return new PendingRegistrationStartResult(PendingRegistrationStartStatus.DuplicateEmailOrPhone);
            }

            existing.ReplaceSignupData(
                passwordHash,
                request.FullName,
                request.PayloadJson,
                request.ProfilePhotoUrl);
            pending = existing;
        }
        else
        {
            pending = new PendingRegistration(
                email,
                phone,
                passwordHash,
                request.FullName,
                request.Role,
                request.PayloadJson,
                request.ProfilePhotoUrl);
            _context.PendingRegistrations.Add(pending);
        }

        var otp = pending.GenerateOtp();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PendingRegistrationStartResult(
            PendingRegistrationStartStatus.Succeeded,
            Map(pending),
            otp);
    }

    public async Task<PendingOtpDispatchResult> ResendOtpAsync(
        string identifier,
        UserRole? expectedRole = null,
        CancellationToken cancellationToken = default)
    {
        var pending = await FindEntityByIdentifierAsync(identifier, cancellationToken);
        if (pending is null)
        {
            return new PendingOtpDispatchResult(PendingOtpDispatchStatus.NotFound);
        }

        if (expectedRole.HasValue && pending.Role != expectedRole.Value)
        {
            return new PendingOtpDispatchResult(PendingOtpDispatchStatus.NotFound);
        }

        if (pending.IsExpired())
        {
            _context.PendingRegistrations.Remove(pending);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PendingOtpDispatchResult(PendingOtpDispatchStatus.Expired);
        }

        if (!pending.CanResendOtp())
        {
            return new PendingOtpDispatchResult(
                PendingOtpDispatchStatus.CooldownActive,
                Map(pending),
                CooldownSecondsRemaining: pending.ResendCooldownSecondsRemaining());
        }

        var otp = pending.GenerateOtp();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new PendingOtpDispatchResult(PendingOtpDispatchStatus.Succeeded, Map(pending), otp);
    }

    public async Task<PendingCompletionResult> VerifyAndCreateAccountAsync(
        string identifier,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        var pending = await FindEntityByIdentifierAsync(identifier, cancellationToken);
        if (pending is null)
        {
            return new PendingCompletionResult(PendingCompletionStatus.NotFound);
        }

        if (pending.IsExpired())
        {
            _context.PendingRegistrations.Remove(pending);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PendingCompletionResult(PendingCompletionStatus.Expired);
        }

        if (!pending.VerifyOtp(otpCode))
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PendingCompletionResult(PendingCompletionStatus.InvalidOtp);
        }

        var createResult = await _identityAccountService.CreateWithPasswordHashAsync(
            new CreateIdentityAccountRequest(
                pending.FullName,
                pending.Email,
                pending.PhoneNumber,
                pending.Role,
                Password: string.Empty,
                pending.ProfilePhotoUrl),
            pending.PasswordHash,
            emailConfirmed: true,
            cancellationToken);

        if (createResult.Status == IdentityCreateStatus.DuplicateEmailOrPhone)
        {
            _context.PendingRegistrations.Remove(pending);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PendingCompletionResult(
                PendingCompletionStatus.Failed,
                Errors: ["USER_ALREADY_EXISTS"]);
        }

        if (createResult.Status != IdentityCreateStatus.Succeeded || createResult.Account is null)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PendingCompletionResult(
                PendingCompletionStatus.Failed,
                Errors: createResult.Errors);
        }

        var payloadJson = pending.PayloadJson;
        var role = pending.Role;
        _context.PendingRegistrations.Remove(pending);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PendingCompletionResult(
            PendingCompletionStatus.Succeeded,
            createResult.Account,
            role,
            payloadJson);
    }

    public async Task<PendingRegistrationSnapshot?> FindByIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var pending = await FindEntityByIdentifierAsync(identifier, cancellationToken);
        return pending is null ? null : Map(pending);
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow;
        return await _context.PendingRegistrations
            .Where(x => x.ExpiresAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<PendingRegistration?> FindEntityByIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        var trimmed = identifier.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var normalizedEmail = trimmed.ToLowerInvariant();
        return await _context.PendingRegistrations
            .FirstOrDefaultAsync(
                x => x.Email == normalizedEmail || x.PhoneNumber == trimmed,
                cancellationToken);
    }

    private string HashPassword(string password)
    {
        var probe = new User("Pending", "pending@zadna.invalid", "0000000000", UserRole.Customer);
        return _userManager.PasswordHasher.HashPassword(probe, password);
    }

    private static PendingRegistrationSnapshot Map(PendingRegistration pending) =>
        new(
            pending.Id,
            pending.FullName,
            pending.Email,
            pending.PhoneNumber,
            pending.Role,
            pending.ProfilePhotoUrl);
}
