using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.UpdateDriverProfile;

public record UpdateDriverProfileCommand(
    Guid DriverId,
    string FullName,
    string Email,
    string PhoneNumber,
    string? VehicleType,
    string? NationalId,
    string? LicenseNumber,
    DateTime? NationalIdExpiryDate,
    DateTime? DriverLicenseExpiryDate,
    string? VehicleLicenseNumber,
    DateTime? VehicleLicenseExpiryDate,
    string? Address,
    string? Region,
    string? City) : IRequest;

public class UpdateDriverProfileCommandValidator : AbstractValidator<UpdateDriverProfileCommand>
{
    public UpdateDriverProfileCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.DriverId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Region).NotEmpty().MaximumLength(50);
        RuleFor(x => x.City).MaximumLength(50);
    }
}

public class UpdateDriverProfileCommandHandler : IRequestHandler<UpdateDriverProfileCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityAccountService _identityAccountService;

    public UpdateDriverProfileCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IIdentityAccountService identityAccountService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _identityAccountService = identityAccountService;
    }

    public async Task Handle(UpdateDriverProfileCommand request, CancellationToken cancellationToken)
    {
        var driver = await _context.Drivers
            .Include(d => d.User)
            .Include(d => d.DocumentReviews)
            .FirstOrDefaultAsync(d => d.Id == request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        DriverVehicleType? parsedVehicleType = null;
        if (!string.IsNullOrWhiteSpace(request.VehicleType))
        {
            if (!DriverVehicleTypeMapper.TryParse(request.VehicleType, out var resolvedVehicleType))
            {
                throw new BusinessRuleException("INVALID_VEHICLE_TYPE", "نوع المركبة غير مدعوم | Unsupported vehicle type.");
            }

            parsedVehicleType = resolvedVehicleType;
        }

        if (string.IsNullOrWhiteSpace(request.Region))
        {
            throw new BusinessRuleException(
                "DRIVER_SERVICE_AREA_REQUIRED",
                "لازم تختار منطقة التشغيل للمندوب.");
        }

        await OperationalGeographyScope.EnsureDriverServiceAreaAsync(
            _context,
            request.Region,
            cancellationToken);

        var nationalId = CoalesceTextUpdate(request.NationalId, driver.NationalId);
        var licenseNumber = CoalesceTextUpdate(request.LicenseNumber, driver.LicenseNumber);
        var vehicleLicenseNumber = CoalesceTextUpdate(request.VehicleLicenseNumber, driver.VehicleLicenseNumber);
        var nationalIdExpiryDate = CoalesceDateUpdate(request.NationalIdExpiryDate, driver.NationalIdExpiryDate);
        var driverLicenseExpiryDate = CoalesceDateUpdate(request.DriverLicenseExpiryDate, driver.DriverLicenseExpiryDate);
        var vehicleLicenseExpiryDate = CoalesceDateUpdate(request.VehicleLicenseExpiryDate, driver.VehicleLicenseExpiryDate);
        var vehicleType = parsedVehicleType ?? driver.VehicleType;

        var personalChanged =
            HasChanged(driver.User.FullName, request.FullName) ||
            HasChanged(driver.User.Email, request.Email) ||
            HasChanged(driver.User.PhoneNumber, request.PhoneNumber) ||
            HasChanged(driver.Address, request.Address);
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
            driver.VehicleType != vehicleType ||
            HasChanged(driver.Region, request.Region) ||
            HasChanged(driver.City, request.City);

        if (personalChanged)
        {
            var updateResult = await _identityAccountService.UpdateProfileAsync(
                driver.UserId,
                request.FullName,
                request.Email,
                request.PhoneNumber,
                cancellationToken);

            if (!updateResult.Succeeded)
            {
                throw new BusinessRuleException(
                    "IDENTITY_PROFILE_UPDATE_FAILED",
                    string.Join(", ", updateResult.Errors ?? []));
            }
        }

        driver.UpdateDetails(
            vehicleType,
            nationalId,
            licenseNumber,
            nationalIdExpiryDate,
            driverLicenseExpiryDate,
            vehicleLicenseNumber,
            vehicleLicenseExpiryDate);

        driver.UpdateAddress(request.Address);
        driver.UpdateServiceArea(request.Region, request.City);

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

        var sensitiveChange = personalChanged || nationalIdChanged || driverLicenseChanged || vehicleLicenseChanged || vehicleChanged;
        if (sensitiveChange)
        {
            driver.RefreshProfileReviewState(
                HasRequiredProfileData(driver),
                sensitiveChange: true,
                note: "Profile details updated by administrator");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void ResetDocumentReviewIfReady(Driver driver, DriverDocumentType type)
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

    private static bool HasRequiredProfileData(Driver driver) =>
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
        !DriverProfileReadinessFactory.HasExpiredRequiredDocuments(driver);

    private static bool HasChanged(string? currentValue, string? requestedValue) =>
        !string.Equals(currentValue?.Trim(), requestedValue?.Trim(), StringComparison.Ordinal);

    private static bool HasChanged(DateTime? currentValue, DateTime? requestedValue) =>
        currentValue?.Date != requestedValue?.Date;

    private static string? CoalesceTextUpdate(string? requestedValue, string? currentValue) =>
        string.IsNullOrWhiteSpace(requestedValue) ? currentValue : requestedValue.Trim();

    private static DateTime? CoalesceDateUpdate(DateTime? requestedValue, DateTime? currentValue) =>
        requestedValue ?? currentValue;
}
