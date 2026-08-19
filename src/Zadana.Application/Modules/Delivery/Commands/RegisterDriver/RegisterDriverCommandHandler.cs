using MediatR;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Geography.Support;
using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.RegisterDriver;

public class RegisterDriverCommandHandler : IRequestHandler<RegisterDriverCommand, AuthResponseDto>
{
    private readonly IPendingRegistrationService _pendingRegistrationService;
    private readonly IRegistrationWorkflow _registrationWorkflow;
    private readonly IApplicationDbContext _context;
    private readonly IOtpService _otpService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RegisterDriverCommandHandler(
        IPendingRegistrationService pendingRegistrationService,
        IRegistrationWorkflow registrationWorkflow,
        IApplicationDbContext context,
        IOtpService otpService,
        IStringLocalizer<SharedResource> localizer)
    {
        _pendingRegistrationService = pendingRegistrationService;
        _registrationWorkflow = registrationWorkflow;
        _context = context;
        _otpService = otpService;
        _localizer = localizer;
    }

    public async Task<AuthResponseDto> Handle(RegisterDriverCommand request, CancellationToken cancellationToken)
    {
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

        var payloadJson = PendingRegistrationPayloadSerializer.Serialize(
            new PendingDriverPayload(
                request.VehicleType,
                request.NationalId,
                request.LicenseNumber,
                request.NationalIdExpiryDate,
                request.DriverLicenseExpiryDate,
                request.VehicleLicenseNumber,
                request.VehicleLicenseExpiryDate,
                request.Address,
                request.Region,
                request.City,
                request.NationalIdFrontImageUrl,
                request.NationalIdBackImageUrl,
                request.LicenseImageUrl,
                request.VehicleImageUrl,
                request.PersonalPhotoUrl));

        var startResult = await _pendingRegistrationService.StartAsync(
            new StartPendingRegistrationRequest(
                request.FullName,
                request.Email,
                request.Phone,
                request.Password,
                UserRole.Driver,
                payloadJson,
                request.PersonalPhotoUrl),
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
            startResult.Pending.Email,
            startResult.PlainOtpCode,
            cancellationToken);

        return _registrationWorkflow.BuildPendingAuthResponse(
            startResult.Pending,
            startResult.RegistrationToken);
    }
}
