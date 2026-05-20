using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.RegisterDriver;

public class RegisterDriverCommandHandler : IRequestHandler<RegisterDriverCommand, AuthResponseDto>
{
    private readonly IRegistrationWorkflow _registrationWorkflow;
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly IAdminAlertService _adminAlertService;

    public RegisterDriverCommandHandler(
        IRegistrationWorkflow registrationWorkflow,
        IDriverRepository driverRepository,
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        IAdminAlertService adminAlertService)
    {
        _registrationWorkflow = registrationWorkflow;
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
        _context = context;
        _adminAlertService = adminAlertService;
    }

    public async Task<AuthResponseDto> Handle(RegisterDriverCommand request, CancellationToken cancellationToken)
    {
        // Validate geography (region + city)
        Guid? regionEntityId = null;
        if (!string.IsNullOrWhiteSpace(request.Region))
        {
            var normalizedRegion = request.Region.Trim().ToUpperInvariant();
            var regionEntity = await _context.SaudiRegions
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Code == normalizedRegion, cancellationToken);

            if (regionEntity is null)
            {
                throw new BusinessRuleException("INVALID_REGION", "المنطقة المختارة غير موجودة | Selected region does not exist.");
            }

            regionEntityId = regionEntity.Id;

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

        var user = await _registrationWorkflow.RegisterAccountAsync(
            new CreateIdentityAccountRequest(
                request.FullName,
                request.Email,
                request.Phone,
                UserRole.Driver,
                request.Password),
            cancellationToken);
        try
        {
            var driver = new Driver(
                user.Id,
                request.VehicleType,
                request.NationalId,
                request.LicenseNumber,
                request.NationalIdExpiryDate,
                request.DriverLicenseExpiryDate,
                request.VehicleLicenseNumber,
                request.VehicleLicenseExpiryDate,
                request.Address,
                request.NationalIdFrontImageUrl,
                request.NationalIdBackImageUrl,
                request.LicenseImageUrl,
                request.VehicleImageUrl,
                request.PersonalPhotoUrl,
                request.Region,
                request.City);

            _driverRepository.Add(driver);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            user = await EnsureDriverAccessScopeAsync(user, driver.Id, cancellationToken);

            user = await _registrationWorkflow.SendRegistrationOtpAsync(user, cancellationToken);

            var authResponse = await _registrationWorkflow.BuildAuthResponseAsync(
                user,
                DriverOperationalStatusFactory.Create(driver),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _adminAlertService.SendAsync(
                new AdminAlertRequest(
                    AdminAlertTypes.DriverApprovalRequested,
                    AdminAlertCategories.Drivers,
                    AdminAlertPriorities.High,
                    "سائق جديد يحتاج مراجعة",
                    "New driver requires review",
                    $"قام السائق {user.FullName} بإرسال طلب الانضمام وبانتظار مراجعة الإدارة.",
                    $"Driver {user.FullName} submitted an onboarding request.",
                    driver.Id,
                    $"/drivers/{driver.Id}",
                    new
                    {
                        driverId = driver.Id,
                        driverUserId = driver.UserId,
                        fullName = user.FullName,
                        region = request.Region,
                        city = request.City,
                        vehicleType = request.VehicleType
                    }),
                cancellationToken);

            return authResponse;
        }
        catch
        {
            await _registrationWorkflow.CompensateAccountCreationFailureAsync(user.Id, cancellationToken);
            throw;
        }
    }

    private async Task<IdentityAccountSnapshot> EnsureDriverAccessScopeAsync(
        IdentityAccountSnapshot account,
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
            return account;
        }

        var existingScope = await _context.UserAccessScopes
            .FirstOrDefaultAsync(scope => scope.UserId == account.Id && scope.IsActive, cancellationToken);

        var userEntity = await _context.Users.FirstOrDefaultAsync(user => user.Id == account.Id, cancellationToken)
            ?? throw new NotFoundException("User", account.Id);

        if (existingScope is null)
        {
            _context.UserAccessScopes.Add(new UserAccessScope(
                account.Id,
                driverRole.Id,
                PanelScope.DriverApp,
                AccessScopeType.DriverSelf,
                driverId));

            userEntity.IncrementPermissionVersion();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
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
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new IdentityAccountSnapshot(
            userEntity.Id,
            userEntity.FullName,
            userEntity.Email,
            userEntity.PhoneNumber,
            userEntity.Role,
            userEntity.PermissionVersion,
            userEntity.AccountStatus,
            userEntity.IsLoginLocked,
            userEntity.LockedAtUtc,
            userEntity.ArchivedAtUtc,
            userEntity.EmailConfirmed,
            userEntity.PhoneNumberConfirmed,
            userEntity.MustChangePassword);
    }
}
