using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
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
        RuleFor(x => x.City).NotEmpty().MaximumLength(50);
    }
}

public class UpdateDriverProfileCommandHandler : IRequestHandler<UpdateDriverProfileCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDriverProfileCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateDriverProfileCommand request, CancellationToken cancellationToken)
    {
        var driver = await _context.Drivers
            .Include(d => d.User)
            .Include(d => d.DocumentReviews)
            .FirstOrDefaultAsync(d => d.Id == request.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        // Unique Email check
        if (!string.Equals(driver.User.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailExists = await _context.Users.AnyAsync(
                u => u.Id != driver.UserId && u.Email == request.Email.ToLowerInvariant().Trim(),
                cancellationToken);
            if (emailExists)
            {
                throw new BusinessRuleException("EMAIL_EXISTS", "البريد الإلكتروني مستخدم بالفعل | Email is already in use.");
            }
        }

        // Unique Phone check
        if (!string.Equals(driver.User.PhoneNumber, request.PhoneNumber, StringComparison.Ordinal))
        {
            var phoneExists = await _context.Users.AnyAsync(
                u => u.Id != driver.UserId && u.PhoneNumber == request.PhoneNumber.Trim(),
                cancellationToken);
            if (phoneExists)
            {
                throw new BusinessRuleException("PHONE_EXISTS", "رقم الجوال مستخدم بالفعل | Phone number is already in use.");
            }
        }

        // Vehicle Type validation
        DriverVehicleType? parsedVehicleType = null;
        if (!string.IsNullOrWhiteSpace(request.VehicleType))
        {
            if (!DriverVehicleTypeMapper.TryParse(request.VehicleType, out var resolvedVehicleType))
            {
                throw new BusinessRuleException("INVALID_VEHICLE_TYPE", "نوع المركبة غير مدعوم | Unsupported vehicle type.");
            }
            parsedVehicleType = resolvedVehicleType;
        }

        // Geography validation (Region & City)
        if (string.IsNullOrWhiteSpace(request.Region) || string.IsNullOrWhiteSpace(request.City))
        {
            throw new BusinessRuleException(
                "DRIVER_SERVICE_AREA_REQUIRED",
                "Driver must choose the region and city they will work in.");
        }

        if (!string.IsNullOrWhiteSpace(request.Region))
        {
            var normalizedRegion = request.Region.Trim().ToUpperInvariant();
            var regionEntity = await _context.SaudiRegions
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Code == normalizedRegion, cancellationToken)
                ?? throw new BusinessRuleException("INVALID_REGION", "المنطقة المختارة غير موجودة | Selected region does not exist.");

            if (!string.IsNullOrWhiteSpace(request.City))
            {
                var normalizedCity = request.City.Trim().ToUpperInvariant();
                var cityExists = await _context.SaudiCities
                    .AsNoTracking()
                    .AnyAsync(c => c.Code == normalizedCity && c.RegionId == regionEntity.Id, cancellationToken);

                if (!cityExists)
                {
                    throw new BusinessRuleException("INVALID_CITY", "المدينة المختارة لا تتبع المنطقة المحددة | Selected city does not belong to the chosen region.");
                }
            }
        }

        // Update entities
        driver.User.UpdateProfile(request.FullName, request.Email, request.PhoneNumber);
        
        driver.UpdateDetails(
            parsedVehicleType,
            request.NationalId,
            request.LicenseNumber,
            request.NationalIdExpiryDate,
            request.DriverLicenseExpiryDate,
            request.VehicleLicenseNumber,
            request.VehicleLicenseExpiryDate);

        driver.UpdateAddress(request.Address);
        driver.UpdateServiceArea(request.Region, request.City);

        // Reset document review status if packet is ready
        ResetDocumentReviewIfReady(driver, DriverDocumentType.NationalId);
        ResetDocumentReviewIfReady(driver, DriverDocumentType.DriverLicense);
        ResetDocumentReviewIfReady(driver, DriverDocumentType.VehicleLicense);

        // Refresh profile review state
        driver.RefreshProfileReviewState(
            HasRequiredProfileData(driver),
            sensitiveChange: true,
            note: "Profile details updated by administrator");

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
        !string.IsNullOrWhiteSpace(driver.City) &&
        !DriverProfileReadinessFactory.HasExpiredRequiredDocuments(driver);
}
