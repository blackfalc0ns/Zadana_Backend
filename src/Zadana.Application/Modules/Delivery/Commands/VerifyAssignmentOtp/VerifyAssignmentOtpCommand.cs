using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.VerifyAssignmentOtp;

public record VerifyAssignmentOtpCommand(
    Guid AssignmentId,
    Guid DriverUserId,
    string OtpType,
    string OtpCode) : IRequest<DriverOtpVerificationResultDto>;

public class VerifyAssignmentOtpCommandValidator : AbstractValidator<VerifyAssignmentOtpCommand>
{
    public VerifyAssignmentOtpCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.OtpCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.OtpType)
            .Must(value => value.Equals("pickup", StringComparison.OrdinalIgnoreCase) || value.Equals("delivery", StringComparison.OrdinalIgnoreCase))
            .WithMessage("OTP type must be pickup or delivery.");
    }
}

public class VerifyAssignmentOtpCommandHandler : IRequestHandler<VerifyAssignmentOtpCommand, DriverOtpVerificationResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDriverRepository _driverRepository;

    public VerifyAssignmentOtpCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IDriverRepository driverRepository)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _driverRepository = driverRepository;
    }

    public async Task<DriverOtpVerificationResultDto> Handle(VerifyAssignmentOtpCommand request, CancellationToken cancellationToken)
    {
        var driver = await _driverRepository.GetByUserIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new BusinessRuleException("DRIVER_NOT_FOUND", "No driver profile found for the current user.");

        if (!driver.CanReceiveOrders)
        {
            throw new BusinessRuleException(
                "DRIVER_NOT_READY_FOR_DISPATCH",
                "Driver must be reviewed and approved by admin before verifying delivery OTP.");
        }

        var assignment = await _context.DeliveryAssignments
            .Include(item => item.Order)
            .FirstOrDefaultAsync(item => item.Id == request.AssignmentId && item.DriverId == driver.Id, cancellationToken)
            ?? throw new BusinessRuleException("ASSIGNMENT_NOT_OWNED", "You can only verify OTP for your assigned delivery.");

        try
        {
            if (request.OtpType.Equals("pickup", StringComparison.OrdinalIgnoreCase))
            {
                assignment.VerifyPickupOtp(driver.Id, request.OtpCode);
            }
            else
            {
                assignment.VerifyDeliveryOtp(driver.Id, request.OtpCode);
            }
        }
        catch (InvalidOperationException ex)
        {
            var errorCode = request.OtpType.Equals("pickup", StringComparison.OrdinalIgnoreCase)
                ? "PICKUP_OTP_INVALID"
                : "DELIVERY_OTP_INVALID";

            throw new BusinessRuleException(errorCode, ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DriverOtpVerificationResultDto(
            assignment.Id,
            assignment.OrderId,
            request.OtpType.ToLowerInvariant(),
            "verified",
            request.OtpType.Equals("pickup", StringComparison.OrdinalIgnoreCase)
                ? "Pickup OTP verified successfully."
                : "Delivery OTP verified successfully.");
    }
}
