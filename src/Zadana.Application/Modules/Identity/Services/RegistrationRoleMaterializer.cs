using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Services;

public sealed class RegistrationRoleMaterializer : IRegistrationRoleMaterializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVendorRepository _vendorRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly ISettlementProcessingSettingsService _settlementProcessingSettingsService;
    private readonly IAdminAlertService _adminAlertService;
    private readonly ILogger<RegistrationRoleMaterializer> _logger;

    public RegistrationRoleMaterializer(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IVendorRepository vendorRepository,
        IDriverRepository driverRepository,
        ISettlementProcessingSettingsService settlementProcessingSettingsService,
        IAdminAlertService adminAlertService,
        ILogger<RegistrationRoleMaterializer> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _vendorRepository = vendorRepository;
        _driverRepository = driverRepository;
        _settlementProcessingSettingsService = settlementProcessingSettingsService;
        _adminAlertService = adminAlertService;
        _logger = logger;
    }

    public async Task MaterializeAsync(
        IdentityAccountSnapshot account,
        UserRole role,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        switch (role)
        {
            case UserRole.Customer:
                await MaterializeCustomerAsync(account, payloadJson, cancellationToken);
                break;
            case UserRole.Vendor:
                await MaterializeVendorAsync(account, payloadJson, cancellationToken);
                break;
            case UserRole.Driver:
                await MaterializeDriverAsync(account, payloadJson, cancellationToken);
                break;
            default:
                throw new BusinessRuleException("UNSUPPORTED_ROLE", $"Role {role} is not supported for pending registration.");
        }
    }

    private async Task MaterializeCustomerAsync(
        IdentityAccountSnapshot account,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var payload = Deserialize<PendingCustomerPayload>(payloadJson);
        AddressLabel? parsedLabel = null;
        if (!string.IsNullOrWhiteSpace(payload.Label) &&
            Enum.TryParse<AddressLabel>(payload.Label, true, out var label))
        {
            parsedLabel = label;
        }

        var address = new CustomerAddress(
            userId: account.Id,
            contactName: account.FullName,
            contactPhone: account.PhoneNumber ?? string.Empty,
            addressLine: payload.AddressLine,
            label: parsedLabel,
            buildingNo: payload.BuildingNo,
            floorNo: payload.FloorNo,
            apartmentNo: payload.ApartmentNo,
            city: payload.City,
            area: payload.Area,
            latitude: payload.Latitude,
            longitude: payload.Longitude);
        address.SetAsDefault();
        _context.CustomerAddresses.Add(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task MaterializeVendorAsync(
        IdentityAccountSnapshot account,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var existingVendor = await _vendorRepository.GetByUserIdAsync(account.Id, cancellationToken);
        if (existingVendor is not null)
        {
            return;
        }
        var payload = Deserialize<PendingVendorPayload>(payloadJson);
        var payoutDay = await _settlementProcessingSettingsService.ResolveConfiguredPayoutDayAsync(
            payload.PayoutDay,
            PayoutScheduleDay.Monday,
            cancellationToken);

        var vendor = new Vendor(
            account.Id,
            payload.BusinessNameAr,
            payload.BusinessNameEn,
            payload.BusinessType,
            payload.CommercialRegistrationNumber,
            payload.ContactEmail,
            payload.ContactPhone,
            payload.TaxId,
            payload.DescriptionAr,
            payload.DescriptionEn,
            payload.OwnerName,
            payload.OwnerEmail,
            payload.OwnerPhone,
            payload.IdNumber,
            payload.Nationality,
            payload.Region,
            payload.City,
            payload.NationalAddress,
            payload.CommercialRegistrationExpiryDate,
            payload.LicenseNumber,
            payload.PayoutCycle,
            payload.LogoUrl,
            payload.CommercialRegisterDocumentUrl,
            payload.TaxDocumentUrl,
            payload.LicenseDocumentUrl,
            payoutDay);

        _vendorRepository.Add(vendor);

        var branch = new VendorBranch(
            vendor.Id,
            payload.BranchName,
            payload.BranchName,
            true,
            payload.BranchAddressLine,
            payload.Region,
            payload.City,
            payload.BranchLatitude,
            payload.BranchLongitude,
            payload.BranchContactPhone,
            string.Empty,
            string.Empty,
            payload.BranchDeliveryRadiusKm);

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
            payload.BankName,
            payload.AccountHolderName,
            payload.Iban,
            payload.SwiftCode);
        bankAccount.MarkAsPreferredForSetup();
        _vendorRepository.AddBankAccount(bankAccount);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await QueueVendorApprovalAlertAsync(vendor, cancellationToken);
    }

    private async Task MaterializeDriverAsync(
        IdentityAccountSnapshot account,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var existingDriver = await _driverRepository.GetByUserIdAsync(account.Id, cancellationToken);
        if (existingDriver is not null)
        {
            await EnsureDriverAccessScopeAsync(account.Id, existingDriver.Id, cancellationToken);
            return;
        }
        var payload = Deserialize<PendingDriverPayload>(payloadJson);
        var driver = new Driver(
            account.Id,
            payload.VehicleType,
            payload.NationalId,
            payload.LicenseNumber,
            payload.NationalIdExpiryDate,
            payload.DriverLicenseExpiryDate,
            payload.VehicleLicenseNumber,
            payload.VehicleLicenseExpiryDate,
            payload.Address,
            payload.NationalIdFrontImageUrl,
            payload.NationalIdBackImageUrl,
            payload.LicenseImageUrl,
            payload.VehicleImageUrl,
            payload.PersonalPhotoUrl,
            payload.Region,
            payload.City);

        driver.UpdatePayoutDay(
            await _settlementProcessingSettingsService.ResolveConfiguredPayoutDayAsync(
                requestedPayoutDay: null,
                fallback: PayoutScheduleDay.Monday,
                cancellationToken: cancellationToken));

        _driverRepository.Add(driver);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await EnsureDriverAccessScopeAsync(account.Id, driver.Id, cancellationToken);
        await QueueDriverApprovalAlertAsync(account, driver, payload, cancellationToken);
    }

    private async Task EnsureDriverAccessScopeAsync(
        Guid userId,
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var driverRole = await _context.RoleDefinitions
            .AsNoTracking()
            .Where(role => role.Code == "driver_account" && role.IsActive)
            .Select(role => new { role.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (driverRole is null)
        {
            return;
        }

        var existingScope = await _context.UserAccessScopes
            .FirstOrDefaultAsync(
                scope => scope.UserId == userId && scope.IsActive && scope.PanelScope == PanelScope.DriverApp,
                cancellationToken);

        var userEntity = await _context.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        if (existingScope is null)
        {
            _context.UserAccessScopes.Add(new UserAccessScope(
                userId,
                driverRole.Id,
                PanelScope.DriverApp,
                AccessScopeType.DriverSelf,
                driverId));
            userEntity.IncrementPermissionVersion();
        }
        else if (existingScope.RoleDefinitionId != driverRole.Id ||
                 existingScope.PanelScope != PanelScope.DriverApp ||
                 existingScope.ScopeType != AccessScopeType.DriverSelf ||
                 existingScope.ScopeEntityId != driverId)
        {
            existingScope.Update(
                driverRole.Id,
                PanelScope.DriverApp,
                AccessScopeType.DriverSelf,
                driverId,
                null);
            userEntity.IncrementPermissionVersion();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
                "Vendor {VendorId} materialised but admin approval alert could not be queued.",
                vendor.Id);
        }
    }

    private async Task QueueDriverApprovalAlertAsync(
        IdentityAccountSnapshot user,
        Driver driver,
        PendingDriverPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await _adminAlertService.SendAsync(
                new AdminAlertRequest(
                    AdminAlertTypes.DriverApprovalRequested,
                    AdminAlertCategories.Drivers,
                    AdminAlertPriorities.High,
                    "مندوب جديد يحتاج مراجعة",
                    "New driver requires review",
                    $"قام المندوب {user.FullName} بإرسال طلب الانضمام وبانتظار مراجعة الإدارة.",
                    $"Driver {user.FullName} submitted an onboarding request.",
                    driver.Id,
                    $"/drivers/{driver.Id}?tab=verification&focus=approval",
                    new
                    {
                        driverId = driver.Id,
                        driverUserId = driver.UserId,
                        fullName = user.FullName,
                        region = payload.Region,
                        city = payload.City,
                        vehicleType = payload.VehicleType
                    }),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Admin alert dispatch failed for new driver {DriverId}. Registration still succeeded.",
                driver.Id);
        }
    }

    private static T Deserialize<T>(string payloadJson) =>
        JsonSerializer.Deserialize<T>(payloadJson, JsonOptions)
        ?? throw new BusinessRuleException("INVALID_PENDING_PAYLOAD", "Pending registration payload is invalid.");
}
