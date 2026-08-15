using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Identity.Commands.RegisterCustomer;

public class RegisterCustomerCommandHandler : IRequestHandler<RegisterCustomerCommand, AuthResponseDto>
{
    private readonly IPendingRegistrationService _pendingRegistrationService;
    private readonly IRegistrationWorkflow _registrationWorkflow;
    private readonly IOtpService _otpService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RegisterCustomerCommandHandler(
        IPendingRegistrationService pendingRegistrationService,
        IRegistrationWorkflow registrationWorkflow,
        IOtpService otpService,
        IStringLocalizer<SharedResource> localizer)
    {
        _pendingRegistrationService = pendingRegistrationService;
        _registrationWorkflow = registrationWorkflow;
        _otpService = otpService;
        _localizer = localizer;
    }

    public async Task<AuthResponseDto> Handle(RegisterCustomerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BusinessRuleException("EMAIL_REQUIRED", _localizer["RequiredField", _localizer["Email"].Value]);
        }

        var payloadJson = PendingRegistrationPayloadSerializer.Serialize(
            new PendingCustomerPayload(
                request.AddressLine,
                request.Label,
                request.BuildingNo,
                request.FloorNo,
                request.ApartmentNo,
                request.City,
                request.Area,
                request.Latitude,
                request.Longitude));

        var startResult = await _pendingRegistrationService.StartAsync(
            new StartPendingRegistrationRequest(
                request.FullName,
                request.Email,
                request.Phone,
                request.Password,
                UserRole.Customer,
                payloadJson,
                request.ProfilePhotoUrl),
            cancellationToken);

        if (startResult.Status == PendingRegistrationStartStatus.DuplicateEmailOrPhone)
        {
            throw new BusinessRuleException("USER_ALREADY_EXISTS", _localizer["USER_ALREADY_EXISTS"]);
        }

        if (startResult.Status != PendingRegistrationStartStatus.Succeeded ||
            startResult.Pending is null ||
            string.IsNullOrWhiteSpace(startResult.PlainOtpCode) ||
            string.IsNullOrWhiteSpace(startResult.RegistrationToken))
        {
            var errors = string.Join(", ", startResult.Errors ?? []);
            throw new BusinessRuleException("CREATION_FAILED", $"{_localizer["CREATION_FAILED"]}: {errors}");
        }

        await _otpService.SendOtpEmailAsync(
            startResult.Pending.OtpDestinationEmail,
            startResult.PlainOtpCode,
            cancellationToken);

        return _registrationWorkflow.BuildPendingAuthResponse(
            startResult.Pending,
            startResult.RegistrationToken);
    }
}
