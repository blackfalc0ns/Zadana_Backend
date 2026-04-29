using MediatR;
using Microsoft.EntityFrameworkCore;
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
                request.Address,
                request.NationalIdFrontImageUrl,
                request.NationalIdBackImageUrl,
                request.LicenseImageUrl,
                request.VehicleImageUrl,
                request.PersonalPhotoUrl,
                request.Region,
                request.City);

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
