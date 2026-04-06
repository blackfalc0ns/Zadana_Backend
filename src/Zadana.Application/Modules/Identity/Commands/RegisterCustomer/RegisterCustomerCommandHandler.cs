using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Localization;

namespace Zadana.Application.Modules.Identity.Commands.RegisterCustomer;

public class RegisterCustomerCommandHandler : IRequestHandler<RegisterCustomerCommand, AuthResponseDto>
{
    private readonly IIdentityAccountService _identityAccountService;
    private readonly IRegistrationWorkflow _registrationWorkflow;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOtpService _otpService;
    private readonly IApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RegisterCustomerCommandHandler(
        IIdentityAccountService identityAccountService,
        IRegistrationWorkflow registrationWorkflow,
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        IApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer)
    {
        _identityAccountService = identityAccountService;
        _registrationWorkflow = registrationWorkflow;
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _context = context;
        _localizer = localizer;
    }

    public async Task<AuthResponseDto> Handle(RegisterCustomerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BusinessRuleException("EMAIL_REQUIRED", _localizer["RequiredField", _localizer["Email"].Value]);
        }

        var createResult = await _identityAccountService.CreateAsync(
            new CreateIdentityAccountRequest(
                request.FullName,
                request.Email,
                request.Phone,
                UserRole.Customer,
                request.Password,
                request.ProfilePhotoUrl),
            cancellationToken);

        if (createResult.Status == IdentityCreateStatus.DuplicateEmailOrPhone)
        {
            throw new BusinessRuleException("USER_ALREADY_EXISTS", _localizer["USER_ALREADY_EXISTS"]);
        }

        if (createResult.Status != IdentityCreateStatus.Succeeded || createResult.Account == null)
        {
            var errors = string.Join(", ", createResult.Errors ?? []);
            throw new BusinessRuleException("CREATION_FAILED", $"{_localizer["CREATION_FAILED"]}: {errors}");
        }

        var user = createResult.Account;
        try
        {
            var otpResult = await _identityAccountService.GenerateRegistrationOtpAsync(user.Id, cancellationToken);
            if (otpResult.Status == OtpDispatchStatus.Failed)
            {
                var errors = string.Join(", ", otpResult.Errors ?? []);
                throw new BusinessRuleException("IDENTITY_OPERATION_FAILED", $"{_localizer["IDENTITY_OPERATION_FAILED"]}: {errors}");
            }

            if (otpResult.Status == OtpDispatchStatus.Succeeded && !string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(otpResult.OtpCode))
            {
                await _otpService.SendOtpEmailAsync(user.Email, otpResult.OtpCode, cancellationToken);
            }

            AddressLabel? parsedLabel = null;
            if (!string.IsNullOrWhiteSpace(request.Label) && Enum.TryParse<AddressLabel>(request.Label, true, out var l))
            {
                parsedLabel = l;
            }

            var address = new CustomerAddress(
                userId: user.Id,
                contactName: user.FullName,
                contactPhone: user.PhoneNumber,
                addressLine: request.AddressLine,
                label: parsedLabel,
                buildingNo: request.BuildingNo,
                floorNo: request.FloorNo,
                apartmentNo: request.ApartmentNo,
                city: request.City,
                area: request.Area,
                latitude: request.Latitude,
                longitude: request.Longitude
            );
            address.SetAsDefault();

            _context.CustomerAddresses.Add(address);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var userDto = new CurrentUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString());
            return new AuthResponseDto(null, userDto, false, _localizer["OtpSentToEmail"]);
        }
        catch
        {
            await _registrationWorkflow.CompensateAccountCreationFailureAsync(user.Id, cancellationToken);
            throw;
        }
    }
}
