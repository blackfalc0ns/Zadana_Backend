using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Api.Modules.Delivery.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Identity.Interfaces;
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
        [FromServices] IIdentityAccountService identityAccountService,
        [FromServices] IApplicationDbContext context,
        [FromServices] IDriverReadService driverReadService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var updateResult = await identityAccountService.UpdateProfileAsync(
            userId,
            request.FullName,
            request.Email,
            request.Phone,
            cancellationToken);

        if (!updateResult.Succeeded)
        {
            throw new BusinessRuleException(
                "IDENTITY_PROFILE_UPDATE_FAILED",
                string.Join(", ", updateResult.Errors ?? Array.Empty<string>()));
        }

        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        driver.UpdateAddress(request.Address);
        driver.RefreshProfileReviewState(HasRequiredProfileData(driver), sensitiveChange: false);

        await context.SaveChangesAsync(cancellationToken);

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
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        DriverVehicleType? parsedVehicleType = null;
        if (!string.IsNullOrWhiteSpace(request.VehicleType))
        {
            if (!DriverVehicleTypeMapper.TryParse(request.VehicleType, out var resolvedVehicleType))
            {
                throw new BusinessRuleException("INVALID_VEHICLE_TYPE", "Unsupported driver vehicle type.");
            }

            parsedVehicleType = resolvedVehicleType;
        }

        driver.UpdateDetails(
            parsedVehicleType,
            request.NationalId,
            request.LicenseNumber);

        if (request.PrimaryZoneId.HasValue)
        {
            var zone = await context.DeliveryZones
                .FirstOrDefaultAsync(z => z.Id == request.PrimaryZoneId.Value && z.IsActive, cancellationToken)
                ?? throw new BusinessRuleException("INVALID_DRIVER_ZONE", "Selected delivery zone is not available.");

            driver.AssignZone(zone.Id, zone);
        }
        else
        {
            driver.ClearZone();
        }

        driver.RefreshProfileReviewState(
            HasRequiredProfileData(driver),
            sensitiveChange: true,
            note: "Profile updated and pending admin re-review");

        await context.SaveChangesAsync(cancellationToken);

        var profile = await driverReadService.GetDriverProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return Ok(profile);
    }

    [HttpPut("documents")]
    public async Task<ActionResult<DriverProfileDto>> UpdateDocuments(
        [FromBody] UpdateDriverDocumentsRequest request,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDriverRepository driverRepository,
        [FromServices] IApplicationDbContext context,
        [FromServices] IDriverReadService driverReadService,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedException("DRIVER_NOT_AUTHENTICATED");
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        driver.UpdateDocuments(
            request.NationalIdImageUrl,
            request.LicenseImageUrl,
            request.VehicleImageUrl,
            request.PersonalPhotoUrl);

        driver.RefreshProfileReviewState(
            HasRequiredProfileData(driver),
            sensitiveChange: true,
            note: "Profile updated and pending admin re-review");

        await context.SaveChangesAsync(cancellationToken);

        var profile = await driverReadService.GetDriverProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Driver", userId);

        return Ok(profile);
    }

    private static bool HasRequiredProfileData(Domain.Modules.Delivery.Entities.Driver driver) =>
        driver.VehicleType is not null &&
        !string.IsNullOrWhiteSpace(driver.NationalId) &&
        !string.IsNullOrWhiteSpace(driver.LicenseNumber) &&
        !string.IsNullOrWhiteSpace(driver.Address) &&
        !string.IsNullOrWhiteSpace(driver.PersonalPhotoUrl) &&
        !string.IsNullOrWhiteSpace(driver.NationalIdImageUrl) &&
        !string.IsNullOrWhiteSpace(driver.LicenseImageUrl) &&
        !string.IsNullOrWhiteSpace(driver.VehicleImageUrl) &&
        driver.PrimaryZoneId.HasValue;
}
