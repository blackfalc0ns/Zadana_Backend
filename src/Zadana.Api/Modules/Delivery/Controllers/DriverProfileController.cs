using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
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

        await profileChangeApprovalService.SubmitAsync(
            userId,
            driver.UserId,
            ProfileChangeApprovalActions.DriverProfilePersonal,
            $"Driver {GetDriverDisplayName(driver)} requested personal profile changes.",
            payload,
            new ProfileChangeApprovalAlert(
                AdminAlertTypes.DriverCriticalChangeSubmitted,
                AdminAlertCategories.Drivers,
                AdminAlertPriorities.High,
                "تعديل بيانات مندوب بانتظار الموافقة",
                "Driver personal change pending approval",
                $"أرسل المندوب {GetDriverDisplayName(driver)} تعديل بيانات شخصية وينتظر موافقة الأدمن.",
                $"Driver {GetDriverDisplayName(driver)} submitted personal profile changes pending admin approval.",
                driver.Id,
                "/admin/access/approvals",
                new { driverId = driver.Id, userId = driver.UserId, section = "personal" }),
            cancellationToken);

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
                "يجب اختيار المنطقة والمدينة التي سيعمل بها المندوب | Driver must choose the region and city they will work in.");
        }

        var normalizedRegion = request.Region.Trim().ToUpperInvariant();
        var regionEntity = await context.SaudiRegions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Code == normalizedRegion, cancellationToken)
            ?? throw new BusinessRuleException("INVALID_REGION", "المنطقة المختارة غير موجودة | Selected region does not exist.");

        var normalizedCity = request.City.Trim().ToUpperInvariant();
        var cityExists = await context.SaudiCities
            .AsNoTracking()
            .AnyAsync(c => c.Code == normalizedCity && c.RegionId == regionEntity.Id, cancellationToken);

        if (!cityExists)
        {
            throw new BusinessRuleException("INVALID_CITY", "المدينة المختارة لا تتبع المنطقة المحددة | Selected city does not belong to the chosen region.");
        }

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

        await profileChangeApprovalService.SubmitAsync(
            userId,
            driver.UserId,
            ProfileChangeApprovalActions.DriverProfileVehicle,
            $"Driver {GetDriverDisplayName(driver)} requested vehicle and identity changes.",
            payload,
            new ProfileChangeApprovalAlert(
                AdminAlertTypes.DriverCriticalChangeSubmitted,
                AdminAlertCategories.Drivers,
                AdminAlertPriorities.High,
                "تعديل بيانات هوية أو مركبة بانتظار الموافقة",
                "Driver vehicle change pending approval",
                $"أرسل المندوب {GetDriverDisplayName(driver)} تعديل بيانات هوية أو مركبة وينتظر موافقة الأدمن.",
                $"Driver {GetDriverDisplayName(driver)} submitted vehicle or identity changes pending admin approval.",
                driver.Id,
                "/admin/access/approvals",
                new { driverId = driver.Id, userId = driver.UserId, section = "vehicle" }),
            cancellationToken);

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

        await profileChangeApprovalService.SubmitAsync(
            userId,
            driver.UserId,
            ProfileChangeApprovalActions.DriverProfileDocuments,
            $"Driver {GetDriverDisplayName(driver)} requested document changes.",
            payload,
            new ProfileChangeApprovalAlert(
                AdminAlertTypes.DriverDocumentsSubmitted,
                AdminAlertCategories.Drivers,
                AdminAlertPriorities.High,
                "مستندات مندوب بانتظار الموافقة",
                "Driver documents pending approval",
                $"أرسل المندوب {GetDriverDisplayName(driver)} مستندات جديدة وينتظر موافقة الأدمن.",
                $"Driver {GetDriverDisplayName(driver)} submitted document changes pending admin approval.",
                driver.Id,
                "/admin/access/approvals",
                new { driverId = driver.Id, userId = driver.UserId, section = "documents" }),
            cancellationToken);

        var profile = await driverReadService.GetDriverProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return Ok(profile);
    }

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
            ? "مستندات سائق جاهزة للمراجعة"
            : "مانع في موافقة سائق";
        var titleEn = hasRequiredProfileData
            ? "Driver documents ready for review"
            : "Driver approval blocker";
        var bodyAr = hasRequiredProfileData
            ? $"قام السائق {driver.User.FullName} بتحديث بياناته ومستنداته."
            : $"بيانات أو مستندات السائق {driver.User.FullName} ما زالت تمنع الموافقة.";
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
