using MediatR;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Support;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.RegisterVendor;

public class RegisterVendorCommandHandler : IRequestHandler<RegisterVendorCommand, AuthResponseDto>
{
    private readonly IRegistrationWorkflow _registrationWorkflow;
    private readonly IPendingRegistrationService _pendingRegistrationService;
    private readonly IVendorRepository _vendorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdminAlertService _adminAlertService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<RegisterVendorCommandHandler> _logger;
    private readonly ISettlementProcessingSettingsService _settlementProcessingSettingsService;
    private readonly IGoogleIdTokenVerifier _googleIdTokenVerifier;
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IOtpService _otpService;

    public RegisterVendorCommandHandler(
        IRegistrationWorkflow registrationWorkflow,
        IPendingRegistrationService pendingRegistrationService,
        IVendorRepository vendorRepository,
        IUnitOfWork unitOfWork,
        IAdminAlertService adminAlertService,
        IApplicationDbContext context,
        ILogger<RegisterVendorCommandHandler> logger,
        ISettlementProcessingSettingsService settlementProcessingSettingsService,
        IGoogleIdTokenVerifier googleIdTokenVerifier,
        IIdentityAccountService identityAccountService,
        IOtpService otpService)
    {
        _registrationWorkflow = registrationWorkflow;
        _pendingRegistrationService = pendingRegistrationService;
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
        _adminAlertService = adminAlertService;
        _context = context;
        _logger = logger;
        _settlementProcessingSettingsService = settlementProcessingSettingsService;
        _googleIdTokenVerifier = googleIdTokenVerifier;
        _identityAccountService = identityAccountService;
        _otpService = otpService;
    }

    public async Task<AuthResponseDto> Handle(RegisterVendorCommand request, CancellationToken cancellationToken)
    {
        await OperationalGeographyScope.EnsureOperationalRegionCityAsync(
            _context,
            request.Region,
            request.City,
            cancellationToken);

        var isGoogleSignup = !string.IsNullOrWhiteSpace(request.GoogleIdToken);
        if (isGoogleSignup)
        {
            return await HandleGoogleSignupAsync(request, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BusinessRuleException("PASSWORD_REQUIRED", "Password is required.");
        }

        var payloadJson = PendingRegistrationPayloadSerializer.Serialize(BuildVendorPayload(request));
        var startResult = await _pendingRegistrationService.StartAsync(
            new StartPendingRegistrationRequest(
                request.FullName,
                request.Email,
                request.Phone,
                request.Password!,
                UserRole.Vendor,
                payloadJson),
            cancellationToken);

        if (startResult.Status == PendingRegistrationStartStatus.DuplicateEmailOrPhone)
        {
            throw new BusinessRuleException("USER_ALREADY_EXISTS", "User already exists.");
        }

        if (startResult.Status != PendingRegistrationStartStatus.Succeeded ||
            startResult.Pending is null ||
            string.IsNullOrWhiteSpace(startResult.PlainOtpCode) ||
            string.IsNullOrWhiteSpace(startResult.RegistrationToken))
        {
            var errors = string.Join(", ", startResult.Errors ?? []);
            throw new BusinessRuleException("CREATION_FAILED", errors);
        }

        await _otpService.SendOtpEmailAsync(
            startResult.Pending.Email,
            startResult.PlainOtpCode,
            cancellationToken);

        return _registrationWorkflow.BuildPendingAuthResponse(
            startResult.Pending,
            startResult.RegistrationToken);
    }

    private async Task<AuthResponseDto> HandleGoogleSignupAsync(
        RegisterVendorCommand request,
        CancellationToken cancellationToken)
    {
        var googleProfile = await _googleIdTokenVerifier.VerifyAsync(request.GoogleIdToken!, cancellationToken);
        if (!string.Equals(googleProfile.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("GOOGLE_EMAIL_MISMATCH", "Google email does not match registration email.");
        }

        var password = GenerateSecurePassword();
        var linkableUserId = await PlatformAccountLinkResolver.ResolveAsync(
            _identityAccountService,
            request.Email.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            UserRole.Vendor,
            cancellationToken);
        var linkedExisting = false;
        IdentityAccountSnapshot user;
        if (linkableUserId == Guid.Empty)
        {
            throw new BusinessRuleException("USER_ALREADY_EXISTS", "User already exists.");
        }

        if (linkableUserId.HasValue)
        {
            var addRole = await _identityAccountService.AddPlatformRoleAsync(
                linkableUserId.Value,
                UserRole.Vendor,
                cancellationToken);
            if (!addRole.Succeeded || addRole.Account is null)
            {
                throw new BusinessRuleException("CREATION_FAILED", string.Join(", ", addRole.Errors ?? []));
            }

            user = addRole.Account;
            linkedExisting = true;
        }
        else
        {
            user = await _registrationWorkflow.RegisterAccountAsync(
                new CreateIdentityAccountRequest(
                    request.FullName,
                    request.Email,
                    request.Phone,
                    UserRole.Vendor,
                    password),
                cancellationToken);
        }

        var confirmResult = await _identityAccountService.ConfirmEmailAsync(user.Id, cancellationToken);
        if (!confirmResult.Succeeded || confirmResult.Account is null)
        {
            if (!linkedExisting)
            {
                await _registrationWorkflow.CompensateAccountCreationFailureAsync(user.Id, cancellationToken);
            }

            throw new BusinessRuleException("IDENTITY_OPERATION_FAILED", "Unable to confirm Google email.");
        }

        user = confirmResult.Account;
        Vendor? vendor = null;
        AuthResponseDto authResponse;

        try
        {
            var payoutDay = await _settlementProcessingSettingsService.ResolveConfiguredPayoutDayAsync(
                request.PayoutDay,
                PayoutScheduleDay.Monday,
                cancellationToken);
            vendor = new Vendor(
                user.Id,
                request.BusinessNameAr,
                request.BusinessNameEn,
                request.BusinessType,
                request.CommercialRegistrationNumber,
                request.ContactEmail,
                request.ContactPhone,
                request.TaxId,
                request.DescriptionAr,
                request.DescriptionEn,
                request.OwnerName,
                request.OwnerEmail,
                request.OwnerPhone,
                request.IdNumber,
                request.Nationality,
                request.Region,
                request.City,
                request.NationalAddress,
                request.CommercialRegistrationExpiryDate,
                request.LicenseNumber,
                request.PayoutCycle,
                request.LogoUrl,
                request.CommercialRegisterDocumentUrl,
                request.TaxDocumentUrl,
                request.LicenseDocumentUrl,
                payoutDay);

            _vendorRepository.Add(vendor);

            var branch = new VendorBranch(
                vendor.Id,
                request.BranchName,
                request.BranchName,
                true,
                request.BranchAddressLine,
                request.Region,
                request.City,
                request.BranchLatitude,
                request.BranchLongitude,
                request.BranchContactPhone,
                string.Empty,
                string.Empty,
                request.BranchDeliveryRadiusKm);

            _vendorRepository.AddBranch(branch);
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 0, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 1, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 2, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 3, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 4, new TimeSpan(9, 0, 0), new TimeSpan(22, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 5, new TimeSpan(9, 0, 0), new TimeSpan(23, 0, 0)));
            branch.OperatingHours.Add(new BranchOperatingHour(branch.Id, 6, new TimeSpan(14, 0, 0), new TimeSpan(23, 30, 0)));

            var bankAccount = new VendorBankAccount(
                vendor.Id,
                request.BankName,
                request.AccountHolderName,
                request.Iban,
                request.SwiftCode);
            bankAccount.MarkAsPreferredForSetup();
            _vendorRepository.AddBankAccount(bankAccount);

            authResponse = await _registrationWorkflow.BuildAuthResponseAsync(
                user,
                cancellationToken: cancellationToken,
                sessionRole: UserRole.Vendor);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (!linkedExisting)
            {
                await _registrationWorkflow.CompensateAccountCreationFailureAsync(user.Id, cancellationToken);
            }

            throw;
        }

        await QueueVendorApprovalAlertAsync(vendor, cancellationToken);
        return authResponse;
    }

    private static PendingVendorPayload BuildVendorPayload(RegisterVendorCommand request) =>
        new(
            request.BusinessNameAr,
            request.BusinessNameEn,
            request.BusinessType,
            request.CommercialRegistrationNumber,
            request.CommercialRegistrationExpiryDate,
            request.ContactEmail,
            request.ContactPhone,
            request.DescriptionAr,
            request.DescriptionEn,
            request.OwnerName,
            request.OwnerEmail,
            request.OwnerPhone,
            request.IdNumber,
            request.Nationality,
            request.Region,
            request.City,
            request.NationalAddress,
            request.TaxId,
            request.LicenseNumber,
            request.BankName,
            request.AccountHolderName,
            request.Iban,
            request.SwiftCode,
            request.PayoutCycle,
            request.LogoUrl,
            request.CommercialRegisterDocumentUrl,
            request.TaxDocumentUrl,
            request.LicenseDocumentUrl,
            request.BranchName,
            request.BranchAddressLine,
            request.BranchLatitude,
            request.BranchLongitude,
            request.BranchContactPhone,
            request.BranchDeliveryRadiusKm,
            request.PayoutDay);

    private static string GenerateSecurePassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@#$%^&*";
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var chars = new char[24];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(chars) + "Aa1!";
    }

    private async Task QueueVendorApprovalAlertAsync(Vendor vendor, CancellationToken cancellationToken)
    {
        try
        {
            await _adminAlertService.SendAsync(
                new AdminAlertRequest(
                    AdminAlertTypes.VendorApprovalRequested,
                    AdminAlertCategories.Vendors,
                    AdminAlertPriorities.High,
                    "تاجر جديد يحتاج مراجعة",
                    "New vendor requires review",
                    $"قام التاجر {vendor.BusinessNameAr} بإرسال طلب الانضمام وبانتظار مراجعة الإدارة.",
                    $"Vendor {vendor.BusinessNameEn} submitted an onboarding request.",
                    vendor.Id,
                    $"/vendors/{vendor.Id}/compliance?focus=review",
                    new
                    {
                        vendorId = vendor.Id,
                        vendorUserId = vendor.UserId,
                        businessNameAr = vendor.BusinessNameAr,
                        businessNameEn = vendor.BusinessNameEn,
                        city = vendor.City,
                        region = vendor.Region
                    }),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Vendor {VendorId} registered but admin approval alert could not be queued.",
                vendor.Id);
        }
    }
}
