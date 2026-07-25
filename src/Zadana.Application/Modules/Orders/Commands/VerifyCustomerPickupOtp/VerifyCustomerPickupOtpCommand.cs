using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.VerifyCustomerPickupOtp;

public record VerifyCustomerPickupOtpCommand(
    Guid OrderId,
    Guid VendorId,
    Guid? BranchId,
    Guid VendorUserId,
    string OtpCode) : IRequest<VerifyCustomerPickupOtpResultDto>;

public record VerifyCustomerPickupOtpResultDto(Guid OrderId, string Status, string Message);

public class VerifyCustomerPickupOtpCommandValidator : AbstractValidator<VerifyCustomerPickupOtpCommand>
{
    public VerifyCustomerPickupOtpCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.VendorUserId).NotEmpty();
        RuleFor(x => x.OtpCode).NotEmpty().MaximumLength(10);
    }
}

public class VerifyCustomerPickupOtpCommandHandler : IRequestHandler<VerifyCustomerPickupOtpCommand, VerifyCustomerPickupOtpResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly OrderInventoryWorkflowService _orderInventoryWorkflowService;

    public VerifyCustomerPickupOtpCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        OrderInventoryWorkflowService orderInventoryWorkflowService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _orderInventoryWorkflowService = orderInventoryWorkflowService;
    }

    public async Task<VerifyCustomerPickupOtpResultDto> Handle(
        VerifyCustomerPickupOtpCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(item =>
                item.Id == request.OrderId &&
                item.VendorId == request.VendorId &&
                (!request.BranchId.HasValue || item.VendorBranchId == request.BranchId.Value),
                cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        if (order.Fulfillment != FulfillmentType.Pickup)
        {
            throw new BusinessRuleException("PICKUP_OTP_NOT_APPLICABLE", "Pickup OTP applies only to pickup orders.");
        }

        if (order.Status == OrderStatus.Delivered && order.PickupOtpVerifiedAtUtc.HasValue)
        {
            return new VerifyCustomerPickupOtpResultDto(
                order.Id,
                "delivered",
                "Order was already picked up by the customer.");
        }

        if (order.Status != OrderStatus.ReadyForPickup)
        {
            throw new BusinessRuleException(
                "INVALID_ORDER_STATUS_TRANSITION",
                $"Cannot verify customer pickup OTP while order is in {order.Status}.");
        }

        var settings = await PlatformPickupSettingsSupport.LoadAsync(_context, cancellationToken);
        var oldStatus = order.Status;

        order.VerifyCustomerPickupOtp(
            request.VendorUserId,
            request.OtpCode,
            settings.PickupOtpMaxAttempts,
            settings.PickupOtpLockoutMinutes);

        // Cash-on-pickup: vendor collected cash at handoff — mark payment Paid so revenue distribution runs.
        if (order.PaymentMethod == PaymentMethodType.CashOnDelivery &&
            order.PaymentStatus != PaymentStatus.Paid)
        {
            order.UpdatePaymentStatus(PaymentStatus.Paid);
            var codPayment = await _context.Payments
                .Where(item => item.OrderId == order.Id)
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            codPayment?.MarkAsPaid();
        }

        _context.OrderStatusHistories.Add(order.StatusHistory.Last());
        await _orderInventoryWorkflowService.ApplyPickupDeductionAsync(order.Id, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException(
                "ORDER_STATE_CHANGED_RETRY",
                "Order state changed while verifying pickup OTP. Please retry.");
        }

        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                OrderStatus.Delivered,
                NotifyCustomer: true,
                NotifyVendor: false,
                ActorRole: "vendor"),
            cancellationToken);

        return new VerifyCustomerPickupOtpResultDto(
            order.Id,
            "delivered",
            "Customer pickup OTP verified and order marked as delivered.");
    }
}
