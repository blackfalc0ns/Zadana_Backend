using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Application.Modules.Delivery.Commands.RegisterDriver;

public class RegisterDriverCommandHandler : IRequestHandler<RegisterDriverCommand, AuthResponseDto>
{
    private readonly IRegistrationWorkflow _registrationWorkflow;
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDriverCommandHandler(
        IRegistrationWorkflow registrationWorkflow,
        IDriverRepository driverRepository,
        IUnitOfWork unitOfWork)
    {
        _registrationWorkflow = registrationWorkflow;
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResponseDto> Handle(RegisterDriverCommand request, CancellationToken cancellationToken)
    {
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

            _driverRepository.Add(driver);
            var authResponse = await _registrationWorkflow.BuildAuthResponseAsync(user, cancellationToken);
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
