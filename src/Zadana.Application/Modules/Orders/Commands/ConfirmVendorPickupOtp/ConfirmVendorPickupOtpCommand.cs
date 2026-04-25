using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.ConfirmVendorPickupOtp;

public record ConfirmVendorPickupOtpCommand(
    Guid OrderId,
    Guid VendorId,
    string OtpCode) : IRequest<VendorPickupOtpConfirmationDto>;

public record VendorPickupOtpConfirmationDto(
    Guid OrderId,
    Guid AssignmentId,
    string Status,
    string Message);

public class ConfirmVendorPickupOtpCommandValidator : AbstractValidator<ConfirmVendorPickupOtpCommand>
{
    public ConfirmVendorPickupOtpCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.OtpCode).NotEmpty().MaximumLength(10);
    }
}

public class ConfirmVendorPickupOtpCommandHandler : IRequestHandler<ConfirmVendorPickupOtpCommand, VendorPickupOtpConfirmationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmVendorPickupOtpCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<VendorPickupOtpConfirmationDto> Handle(ConfirmVendorPickupOtpCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(item => item.Id == request.OrderId && item.VendorId == request.VendorId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        var assignment = await _context.DeliveryAssignments
            .FirstOrDefaultAsync(item => item.OrderId == order.Id && item.DriverId != null, cancellationToken)
            ?? throw new BusinessRuleException("NO_ASSIGNED_DRIVER", "No assigned driver was found for this order.");

        if (!assignment.DriverId.HasValue)
        {
            throw new BusinessRuleException("NO_ASSIGNED_DRIVER", "No assigned driver was found for this order.");
        }

        try
        {
            assignment.VerifyPickupOtp(assignment.DriverId.Value, request.OtpCode);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException("PICKUP_OTP_INVALID", ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VendorPickupOtpConfirmationDto(
            order.Id,
            assignment.Id,
            "verified",
            "Pickup OTP confirmed successfully.");
    }
}
