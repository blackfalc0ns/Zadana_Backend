using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Application.Modules.Identity.Services;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/drivers/me/profile")]
[Tags("Driver App API")]
[Authorize(Policy = "DriverOnly")]
public class DriverProfileController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DriverProfileDto>> GetProfile(
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverReadService driverReadService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var profile = await driverReadService.GetDriverProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return Ok(profile);
    }

    [HttpPut("personal")]
    public async Task<ActionResult<DriverProfileDto>> UpdatePersonal(
        [FromBody] UpdateDriverPersonalProfileRequest request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IDriverReadService driverReadService,
        [FromServices] IProfileChangeApprovalService profileChangeApprovalService,
        [FromServices] IIdentityAccountService identityAccountService,
        [FromServices] IUnitOfWork unitOfWork,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdWithReviewsAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        var payload = new DriverPersonalProfileChangePayload(
            driver.Id,
            request.FullName,
            request.Email,
            request.Phone,
            request.Address);

        if (RequiresApprovalWorkflow(driver))
        {
            await profileChangeApprovalService.SubmitAsync(
                userId,
                driver.UserId,
                ProfileChangeApprovalActions.DriverProfilePersonal,
                $"Driver {GetDriverDisplayName(driver)} requested personal profile changes.",
                payload,
                BuildPersonalApprovalAlert(driver),
                cancellationToken);
        }
        else
        {
            var updateResult = await identityAccountService.UpdateProfileAsync(
                driver.UserId,
                request.FullName,
                request.Email,
                request.Phone,
                cancellationToken);
            if (!updateResult.Succeeded)
            {
                throw new BusinessRuleException(
                    "IDENTITY_PROFILE_UPDATE_FAILED",
                    string.Join(", ", updateResult.Errors ?? []));
            }

            driver.UpdateAddress(request.Address);
            driver.RefreshProfileReviewState(
                HasRequiredProfileData(driver),
                sensitiveChange: true,
                note: "Personal profile updated during onboarding");
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var profile = await driverReadService.GetDriverProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return Ok(profile);
    }

    [HttpPut("vehicle")]
    public async Task<ActionResult<DriverProfileDto>> UpdateVehicle(
        [FromBody] UpdateDriverVehicleProfileRequest request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] IDriverReadService driverReadService,
        [FromServices] IProfileChangeApprovalService profileChangeApprovalService,
        [FromServices] IUnitOfWork unitOfWork,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdWithReviewsAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        DriverVehicleType? parsedVehicleType = null;
        if (!string.IsNullOrWhiteSpace(request.VehicleType))
        {
            if (!DriverVehicleTypeMapper.TryParse(request.VehicleType, out var resolvedVehicleType))
            {
                throw new BusinessRuleException("INVALID_VEHICLE_TYPE", "نوع المركبة غير مدعوم | Unsupported vehicle type.");
            }

            parsedVehicleType = resolvedVehicleType;
        }

        if (string.IsNullOrWhiteSpace(request.Region) || string.IsNullOrWhiteSpace(request.City))
        {
            throw new BusinessRuleException(
                "DRIVER_SERVICE_AREA_REQUIRED",
                "لازم تختار منطقة ومدينة التشغيل للمندوب.");
        }

        await OperationalGeographyScope.EnsureDriverServiceAreaAsync(
            context,
            request.Region,
            request.City,
            cancellationToken);

        var payload = new DriverVehicleProfileChangePayload(
            driver.Id,
            request.VehicleType,
            request.NationalId,
            request.LicenseNumber,
            request.NationalIdExpiryDate,
            request.DriverLicenseExpiryDate,
            request.VehicleLicenseNumber,
            request.VehicleLicenseExpiryDate,
            request.Region,
            request.City);

        if (RequiresApprovalWorkflow(driver))
        {
            await profileChangeApprovalService.SubmitAsync(
                userId,
                driver.UserId,
                ProfileChangeApprovalActions.DriverProfileVehicle,
                $"Driver {GetDriverDisplayName(driver)} requested vehicle and identity changes.",
                payload,
                BuildVehicleApprovalAlert(driver),
                cancellationToken);
        }
        else
        {
            ApplyVehicleProfileChanges(
                driver,
                parsedVehicleType,
                request.NationalId,
                request.LicenseNumber,
                request.NationalIdExpiryDate,
                request.DriverLicenseExpiryDate,
                request.VehicleLicenseNumber,
                request.VehicleLicenseExpiryDate,
                request.Region,
                request.City);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var profile = await driverReadService.GetDriverProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return Ok(profile);
    }

    [HttpPut("documents")]
    public async Task<ActionResult<DriverProfileDto>> UpdateDocuments(
        [FromBody] UpdateDriverDocumentsRequest request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IDriverReadService driverReadService,
        [FromServices] IProfileChangeApprovalService profileChangeApprovalService,
        [FromServices] IUnitOfWork unitOfWork,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdWithReviewsAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        var payload = new DriverDocumentsProfileChangePayload(
            driver.Id,
            request.NationalIdFrontImageUrl,
            request.NationalIdBackImageUrl,
            request.LicenseImageUrl,
            request.VehicleImageUrl,
            request.PersonalPhotoUrl);

        if (RequiresApprovalWorkflow(driver))
        {
            await profileChangeApprovalService.SubmitAsync(
                userId,
                driver.UserId,
                ProfileChangeApprovalActions.DriverProfileDocuments,
                $"Driver {GetDriverDisplayName(driver)} requested document changes.",
                payload,
                BuildDocumentsApprovalAlert(driver),
                cancellationToken);
        }
        else
        {
            ApplyDocumentProfileChanges(
                driver,
                request.NationalIdFrontImageUrl,
                request.NationalIdBackImageUrl,
                request.LicenseImageUrl,
                request.VehicleImageUrl,
                request.PersonalPhotoUrl);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var profile = await driverReadService.GetDriverProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return Ok(profile);
    }

    private static bool RequiresApprovalWorkflow(Domain.Modules.Delivery.Entities.Driver driver) =>
        driver.VerificationStatus == DriverVerificationStatus.Approved;

    private static void ApplyVehicleProfileChanges(
        Domain.Modules.Delivery.Entities.Driver driver,
        DriverVehicleType? parsedVehicleType,
        string? nationalId,
        string? licenseNumber,
        DateTime? nationalIdExpiryDate,
        DateTime? driverLicenseExpiryDate,
        string? vehicleLicenseNumber,
        DateTime? vehicleLicenseExpiryDate,
        string? region,
        string? city)
    {
        var nationalIdChanged =
            HasChanged(driver.NationalId, nationalId) ||
            HasChanged(driver.NationalIdExpiryDate, nationalIdExpiryDate);
        var driverLicenseChanged =
            HasChanged(driver.LicenseNumber, licenseNumber) ||
            HasChanged(driver.DriverLicenseExpiryDate, driverLicenseExpiryDate);
        var vehicleLicenseChanged =
            HasChanged(driver.VehicleLicenseNumber, vehicleLicenseNumber) ||
            HasChanged(driver.VehicleLicenseExpiryDate, vehicleLicenseExpiryDate);
        var vehicleChanged =
            driver.VehicleType != parsedVehicleType ||
            HasChanged(driver.Region, region) ||
            HasChanged(driver.City, city);

        driver.UpdateDetails(
            parsedVehicleType,
            nationalId,
            licenseNumber,
            nationalIdExpiryDate,
            driverLicenseExpiryDate,
            vehicleLicenseNumber,
            vehicleLicenseExpiryDate);
        driver.UpdateServiceArea(region, city);

        if (nationalIdChanged)
        {
            ResetDocumentReviewIfReady(driver, DriverDocumentType.NationalId);
        }

        if (driverLicenseChanged)
        {
            ResetDocumentReviewIfReady(driver, DriverDocumentType.DriverLicense);
        }

        if (vehicleLicenseChanged)
        {
            ResetDocumentReviewIfReady(driver, DriverDocumentType.VehicleLicense);
        }

        if (nationalIdChanged || driverLicenseChanged || vehicleLicenseChanged || vehicleChanged)
        {
            driver.RefreshProfileReviewState(
                HasRequiredProfileData(driver),
                sensitiveChange: true,
                note: "Vehicle profile updated during onboarding");
        }
    }

    private static void ApplyDocumentProfileChanges(
        Domain.Modules.Delivery.Entities.Driver driver,
        string? nationalIdFrontImageUrl,
        string? nationalIdBackImageUrl,
        string? licenseImageUrl,
        string? vehicleImageUrl,
        string? personalPhotoUrl)
    {
        var nationalIdChanged =
            HasChanged(driver.NationalIdFrontImageUrl, nationalIdFrontImageUrl) ||
            HasChanged(driver.NationalIdBackImageUrl, nationalIdBackImageUrl);
        var driverLicenseChanged = HasChanged(driver.LicenseImageUrl, licenseImageUrl);
        var vehicleLicenseChanged = HasChanged(driver.VehicleImageUrl, vehicleImageUrl);
        var personalPhotoChanged = HasChanged(driver.PersonalPhotoUrl, personalPhotoUrl);

        driver.UpdateDocuments(
            nationalIdFrontImageUrl,
            nationalIdBackImageUrl,
            licenseImageUrl,
            vehicleImageUrl,
            personalPhotoUrl);

        if (nationalIdChanged)
        {
            ResetDocumentReviewIfReady(driver, DriverDocumentType.NationalId);
        }

        if (driverLicenseChanged)
        {
            ResetDocumentReviewIfReady(driver, DriverDocumentType.DriverLicense);
        }

        if (vehicleLicenseChanged)
        {
            ResetDocumentReviewIfReady(driver, DriverDocumentType.VehicleLicense);
        }

        if (nationalIdChanged || driverLicenseChanged || vehicleLicenseChanged || personalPhotoChanged)
        {
            driver.RefreshProfileReviewState(
                HasRequiredProfileData(driver),
                sensitiveChange: true,
                note: "Documents updated during onboarding");
        }
    }

    private static ProfileChangeApprovalAlert BuildPersonalApprovalAlert(Domain.Modules.Delivery.Entities.Driver driver) =>
        new(
            AdminAlertTypes.DriverCriticalChangeSubmitted,
            AdminAlertCategories.Drivers,
            AdminAlertPriorities.High,
            "تعديل بيانات مندوب بانتظار الاعتماد",
            "Driver personal change pending approval",
            $"أرسل المندوب {GetDriverDisplayName(driver)} تعديل بيانات شخصية وينتظر اعتماد المشرف.",
            $"Driver {GetDriverDisplayName(driver)} submitted personal profile changes pending admin approval.",
            driver.Id,
            "/admin/access/approvals",
            new { driverId = driver.Id, userId = driver.UserId, section = "personal" });

    private static ProfileChangeApprovalAlert BuildVehicleApprovalAlert(Domain.Modules.Delivery.Entities.Driver driver) =>
        new(
            AdminAlertTypes.DriverCriticalChangeSubmitted,
            AdminAlertCategories.Drivers,
            AdminAlertPriorities.High,
            "تعديل بيانات هوية أو مركبة بانتظار الاعتماد",
            "Driver vehicle change pending approval",
            $"أرسل المندوب {GetDriverDisplayName(driver)} تعديل بيانات هوية أو مركبة وينتظر اعتماد المشرف.",
            $"Driver {GetDriverDisplayName(driver)} submitted vehicle or identity changes pending admin approval.",
            driver.Id,
            "/admin/access/approvals",
            new { driverId = driver.Id, userId = driver.UserId, section = "vehicle" });

    private static ProfileChangeApprovalAlert BuildDocumentsApprovalAlert(Domain.Modules.Delivery.Entities.Driver driver) =>
        new(
            AdminAlertTypes.DriverDocumentsSubmitted,
            AdminAlertCategories.Drivers,
            AdminAlertPriorities.High,
            "مستندات مندوب بانتظار الاعتماد",
            "Driver documents pending approval",
            $"أرسل المندوب {GetDriverDisplayName(driver)} مستندات جديدة وينتظر اعتماد المشرف.",
            $"Driver {GetDriverDisplayName(driver)} submitted document changes pending admin approval.",
            driver.Id,
            $"/drivers/{driver.Id}?tab=verification",
            new { driverId = driver.Id, userId = driver.UserId, section = "documents" });

    private static bool HasChanged(string? currentValue, string? requestedValue) =>
        !string.Equals(currentValue?.Trim(), requestedValue?.Trim(), StringComparison.Ordinal);

    private static bool HasChanged(DateTime? currentValue, DateTime? requestedValue) =>
        currentValue?.Date != requestedValue?.Date;

    private static bool HasRequiredProfileData(Domain.Modules.Delivery.Entities.Driver driver) =>
        driver.VehicleType is not null &&
        !string.IsNullOrWhiteSpace(driver.NationalId) &&
        !string.IsNullOrWhiteSpace(driver.LicenseNumber) &&
        !string.IsNullOrWhiteSpace(driver.VehicleLicenseNumber) &&
        !string.IsNullOrWhiteSpace(driver.Address) &&
        !string.IsNullOrWhiteSpace(driver.PersonalPhotoUrl) &&
        !string.IsNullOrWhiteSpace(driver.NationalIdFrontImageUrl) &&
        !string.IsNullOrWhiteSpace(driver.NationalIdBackImageUrl) &&
        !string.IsNullOrWhiteSpace(driver.LicenseImageUrl) &&
        !string.IsNullOrWhiteSpace(driver.VehicleImageUrl) &&
        driver.NationalIdExpiryDate.HasValue &&
        driver.DriverLicenseExpiryDate.HasValue &&
        driver.VehicleLicenseExpiryDate.HasValue &&
        !string.IsNullOrWhiteSpace(driver.Region) &&
        !string.IsNullOrWhiteSpace(driver.City) &&
        !DriverProfileReadinessFactory.HasExpiredRequiredDocuments(driver);

    private static void ResetDocumentReviewIfReady(Domain.Modules.Delivery.Entities.Driver driver, DriverDocumentType type)
    {
        var hasPacket = type switch
        {
            DriverDocumentType.NationalId => DriverProfileReadinessFactory.HasNationalIdPacket(driver),
            DriverDocumentType.DriverLicense => DriverProfileReadinessFactory.HasDriverLicensePacket(driver),
            DriverDocumentType.VehicleLicense => DriverProfileReadinessFactory.HasVehicleLicensePacket(driver),
            _ => false
        };

        if (hasPacket)
        {
            driver.ResetDocumentReviewToPending(type);
        }
    }

    private static Task SendDriverReviewAlertAsync(
        Domain.Modules.Delivery.Entities.Driver driver,
        IAdminAlertService adminAlertService,
        bool hasRequiredProfileData,
        CancellationToken cancellationToken)
    {
        var type = hasRequiredProfileData
            ? AdminAlertTypes.DriverDocumentsSubmitted
            : AdminAlertTypes.DriverApprovalBlocked;
        var priority = hasRequiredProfileData
            ? AdminAlertPriorities.High
            : AdminAlertPriorities.Critical;
        var titleAr = hasRequiredProfileData
            ? "مستندات مندوب جاهزة للمراجعة"
            : "مانع اعتماد مندوب";
        var titleEn = hasRequiredProfileData
            ? "Driver documents ready for review"
            : "Driver approval blocker";
        var bodyAr = hasRequiredProfileData
            ? $"قام المندوب {driver.User.FullName} بتحديث بياناته ومستنداته."
            : $"بيانات أو مستندات المندوب {driver.User.FullName} ما زالت تمنع الاعتماد.";
        var bodyEn = hasRequiredProfileData
            ? $"Driver {driver.User.FullName} updated profile data and documents."
            : $"Driver {driver.User.FullName} still has profile or document blockers.";

        return adminAlertService.SendAsync(
            new AdminAlertRequest(
                type,
                AdminAlertCategories.Drivers,
                priority,
                titleAr,
                titleEn,
                bodyAr,
                bodyEn,
                driver.Id,
                $"/drivers/{driver.Id}",
                new
                {
                    driverId = driver.Id,
                    userId = driver.UserId,
                    status = driver.Status.ToString(),
                    hasRequiredProfileData
                }),
            cancellationToken);
    }

    private static string GetDriverDisplayName(Domain.Modules.Delivery.Entities.Driver driver) =>
        string.IsNullOrWhiteSpace(driver.User?.FullName)
            ? driver.UserId.ToString("N")
            : driver.User.FullName.Trim();
}
