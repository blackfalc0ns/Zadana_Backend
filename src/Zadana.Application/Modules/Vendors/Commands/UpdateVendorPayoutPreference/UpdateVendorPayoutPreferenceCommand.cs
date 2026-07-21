using FluentValidation;
using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;
using Zadana.Application.Modules.Vendors.DTOs;
using Zadana.Application.Modules.Vendors.Interfaces;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Vendors.Commands.UpdateVendorPayoutPreference;

/// <summary>
/// Updates only the vendor's scheduled payout day. This remains independent from
/// sensitive banking-data changes, which continue through the approval workflow.
/// </summary>
public record UpdateVendorPayoutPreferenceCommand(string? PayoutDay) : IRequest<VendorPayoutPreferenceDto>;

public sealed class UpdateVendorPayoutPreferenceCommandValidator
    : AbstractValidator<UpdateVendorPayoutPreferenceCommand>
{
    public UpdateVendorPayoutPreferenceCommandValidator()
    {
        RuleFor(x => x.PayoutDay)
            .NotEmpty()
            .Must(value => PayoutScheduleDayPolicy.TryParse(value, out _))
            .WithErrorCode("INVALID_PAYOUT_DAY")
            .WithMessage("Payout day must be a valid day of the week.");
    }
}

public sealed class UpdateVendorPayoutPreferenceCommandHandler
    : IRequestHandler<UpdateVendorPayoutPreferenceCommand, VendorPayoutPreferenceDto>
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISettlementProcessingSettingsService _settlementProcessingSettingsService;

    public UpdateVendorPayoutPreferenceCommandHandler(
        IVendorRepository vendorRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ISettlementProcessingSettingsService settlementProcessingSettingsService)
    {
        _vendorRepository = vendorRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _settlementProcessingSettingsService = settlementProcessingSettingsService;
    }

    public async Task<VendorPayoutPreferenceDto> Handle(
        UpdateVendorPayoutPreferenceCommand request,
        CancellationToken cancellationToken)
    {
        if (!PayoutScheduleDayPolicy.TryParse(request.PayoutDay, out var payoutDay))
        {
            throw new BadRequestException(
                "INVALID_PAYOUT_DAY",
                "Payout day must be a valid day of the week.");
        }

        await _settlementProcessingSettingsService.EnsurePayoutDayEnabledAsync(
            payoutDay,
            cancellationToken);

        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedException("USER_NOT_AUTHENTICATED");

        var vendor = await _vendorRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Vendor", userId);

        // This intentionally does not read or mutate any bank account. Older
        // vendors can choose a schedule before their banking profile is complete.
        vendor.UpdatePayoutDay(payoutDay);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VendorPayoutPreferenceDto(
            vendor.PayoutDay.ToString(),
            (await _settlementProcessingSettingsService.GetEnabledPayoutDaysAsync(cancellationToken))
                .Select(day => day.ToString())
                .ToArray());
    }
}
