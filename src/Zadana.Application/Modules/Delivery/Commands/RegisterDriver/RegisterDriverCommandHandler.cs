using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
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

    public RegisterDriverCommandHandler(
        IRegistrationWorkflow registrationWorkflow,
        IDriverRepository driverRepository,
        IUnitOfWork unitOfWork,
        IApplicationDbContext context)
    {
        _registrationWorkflow = registrationWorkflow;
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<AuthResponseDto> Handle(RegisterDriverCommand request, CancellationToken cancellationToken)
    {
        var zone = await _context.DeliveryZones.FindAsync([request.PrimaryZoneId], cancellationToken);

        if (zone is null)
        {
            throw new NotFoundException("DeliveryZone", request.PrimaryZoneId);
        }

        if (!zone.IsActive)
        {
            throw new BusinessRuleException("DELIVERY_ZONE_NOT_ACTIVE", "Selected delivery zone is not active.");
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
                request.Address,
                request.NationalIdImageUrl,
                request.LicenseImageUrl,
                request.VehicleImageUrl,
                request.PersonalPhotoUrl);
            driver.AssignZone(zone.Id, zone);

            _driverRepository.Add(driver);
            var authResponse = await _registrationWorkflow.BuildAuthResponseAsync(
                user,
                DriverOperationalStatusFactory.Create(driver),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return authResponse;
        }
        catch
        {
            await _registrationWorkflow.CompensateAccountCreationFailureAsync(user.Id, cancellationToken);
            throw;
        }
    }
}
