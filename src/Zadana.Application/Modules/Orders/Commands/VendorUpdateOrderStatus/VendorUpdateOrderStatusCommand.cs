using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.VendorUpdateOrderStatus;

public record VendorUpdateOrderStatusCommand(
    Guid OrderId,
    Guid VendorId,
    Guid? BranchId,
    OrderStatus NewStatus,
    string? Note) : IRequest<VendorUpdateOrderStatusResultDto>;

public record VendorUpdateOrderStatusResultDto(Guid OrderId, string Status, string Message);

public class VendorUpdateOrderStatusCommandValidator : AbstractValidator<VendorUpdateOrderStatusCommand>
{
    private static readonly OrderStatus[] AllowedVendorStatuses =
    [
        OrderStatus.Accepted,
        OrderStatus.VendorRejected,
        OrderStatus.Preparing,
        OrderStatus.ReadyForPickup
    ];

    public VendorUpdateOrderStatusCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.NewStatus)
            .Must(status => AllowedVendorStatuses.Contains(status))
            .WithMessage("Vendor can only set status to: Accepted, VendorRejected, Preparing, ReadyForPickup");
    }
}

public class VendorUpdateOrderStatusCommandHandler : IRequestHandler<VendorUpdateOrderStatusCommand, VendorUpdateOrderStatusResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly IOrderStatusNotificationDispatcher _orderStatusNotificationDispatcher;
    private readonly IDeliveryDispatchService _deliveryDispatchService;
    private readonly OrderInventoryWorkflowService _orderInventoryWorkflowService;

    public VendorUpdateOrderStatusCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        IOrderStatusNotificationDispatcher orderStatusNotificationDispatcher,
        IDeliveryDispatchService deliveryDispatchService,
        OrderInventoryWorkflowService? orderInventoryWorkflowService = null)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _orderStatusNotificationDispatcher = orderStatusNotificationDispatcher;
        _deliveryDispatchService = deliveryDispatchService;
        _orderInventoryWorkflowService = orderInventoryWorkflowService ?? new OrderInventoryWorkflowService(context);
    }

    public async Task<VendorUpdateOrderStatusResultDto> Handle(VendorUpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x =>
                x.Id == request.OrderId &&
                x.VendorId == request.VendorId &&
                (!request.BranchId.HasValue || x.VendorBranchId == request.BranchId.Value),
                cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        // Pickup orders wrongly advanced by the courier dispatch worker must be healed back
        // to ReadyForPickup when the merchant re-confirms readiness.
        if (request.NewStatus == OrderStatus.ReadyForPickup &&
            order.Fulfillment == FulfillmentType.Pickup &&
            order.Status is OrderStatus.DriverAssignmentInProgress
                or OrderStatus.DriverAssigned
                or OrderStatus.PickedUp
                or OrderStatus.OnTheWay)
        {
            EnsureVendorCanActOnPayment(order.PaymentMethod, order.PaymentStatus);
            var healOldStatus = order.Status;
            order.ChangeStatus(OrderStatus.ReadyForPickup, null, "Restored customer-pickup readiness");
            _context.OrderStatusHistories.Add(order.StatusHistory.Last());
            var pickupSettings = await PlatformPickupSettingsSupport.LoadAsync(_context, cancellationToken);
            order.MarkReadyForCustomerPickup(
                PlatformPickupSettingsSupport.ResolveOtpTtl(pickupSettings),
                PlatformPickupSettingsSupport.ResolveNoShowTimeout(pickupSettings));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _publisher.Publish(
                new OrderStatusChangedNotification(
                    order.Id,
                    order.UserId,
                    order.VendorId,
                    order.OrderNumber,
                    healOldStatus,
                    OrderStatus.ReadyForPickup,
                    NotifyCustomer: true,
                    NotifyVendor: false,
                    ActorRole: "vendor"),
                cancellationToken);

            return new VendorUpdateOrderStatusResultDto(
                order.Id,
                order.Status.ToString(),
                "Order status updated successfully");
        }

        // Idempotent: avoid duplicate status side effects, but still retry dispatch for ready orders.
        // Once an order is marked ready, auto-dispatch may immediately advance it to a later
        // dispatch state before the vendor UI refreshes.
        if (IsIdempotentReadyTransition(order.Status, request.NewStatus))
        {
            EnsureVendorCanActOnPayment(order.PaymentMethod, order.PaymentStatus);

            if (request.NewStatus == OrderStatus.ReadyForPickup && order.Fulfillment == FulfillmentType.Pickup)
            {
                var pickupSettings = await PlatformPickupSettingsSupport.LoadAsync(_context, cancellationToken);
                order.MarkReadyForCustomerPickup(
                    PlatformPickupSettingsSupport.ResolveOtpTtl(pickupSettings),
                    PlatformPickupSettingsSupport.ResolveNoShowTimeout(pickupSettings));
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else if (request.NewStatus == OrderStatus.ReadyForPickup && order.Fulfillment != FulfillmentType.Pickup)
            {
                await _deliveryDispatchService.TryAutoDispatchAsync(order.Id, cancellationToken: cancellationToken);
            }

            return new VendorUpdateOrderStatusResultDto(
                order.Id,
                order.Status.ToString(),
                "Order status updated successfully");
        }

        ValidateTransition(order.Status, request.NewStatus);
        EnsureVendorCanActOnPayment(order.PaymentMethod, order.PaymentStatus);

        var oldStatus = order.Status;
        order.ChangeStatus(request.NewStatus, null, request.Note);
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());
        if (request.NewStatus == OrderStatus.VendorRejected)
        {
            await _orderInventoryWorkflowService.ApplyRestockAsync(order.Id, "vendor_rejected", cancellationToken);
        }

        if (request.NewStatus == OrderStatus.ReadyForPickup && order.Fulfillment == FulfillmentType.Pickup)
        {
            var pickupSettings = await PlatformPickupSettingsSupport.LoadAsync(_context, cancellationToken);
            order.MarkReadyForCustomerPickup(
                PlatformPickupSettingsSupport.ResolveOtpTtl(pickupSettings),
                PlatformPickupSettingsSupport.ResolveNoShowTimeout(pickupSettings));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _orderStatusNotificationDispatcher.DispatchCustomerAsync(
            new OrderStatusCustomerNotificationRequest(
                order.UserId,
                order.Id,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                request.NewStatus,
                ActorRole: "vendor",
                Fulfillment: order.Fulfillment),
            cancellationToken);

        // Publish notification event
        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                request.NewStatus,
                NotifyCustomer: true,
                NotifyVendor: false,
                ActorRole: "vendor",
                CustomerNotificationAlreadySent: true),
            cancellationToken);

        if (request.NewStatus == OrderStatus.ReadyForPickup && order.Fulfillment != FulfillmentType.Pickup)
        {
            await _deliveryDispatchService.TryAutoDispatchAsync(order.Id, cancellationToken: cancellationToken);
        }

        return new VendorUpdateOrderStatusResultDto(
            order.Id,
            request.NewStatus.ToString(),
            "Order status updated successfully");
    }

    private static void ValidateTransition(OrderStatus current, OrderStatus target)
    {
        var valid = (current, target) switch
        {
            (OrderStatus.PendingVendorAcceptance, OrderStatus.Accepted) => true,
            (OrderStatus.PendingVendorAcceptance, OrderStatus.VendorRejected) => true,
            (OrderStatus.Accepted, OrderStatus.Preparing) => true,
            (OrderStatus.Preparing, OrderStatus.ReadyForPickup) => true,
            _ => false
        };

        if (!valid)
        {
            throw new BusinessRuleException(
                "INVALID_ORDER_STATUS_TRANSITION",
                $"Cannot transition from {current} to {target}");
        }
    }

    private static bool IsIdempotentReadyTransition(OrderStatus current, OrderStatus target) =>
        current == target ||
        (target == OrderStatus.ReadyForPickup &&
         current is OrderStatus.DriverAssignmentInProgress or OrderStatus.DriverAssigned);

    private static void EnsureVendorCanActOnPayment(PaymentMethodType paymentMethod, PaymentStatus paymentStatus)
    {
        if (paymentMethod is (PaymentMethodType.Card or PaymentMethodType.BankTransfer) &&
            paymentStatus != PaymentStatus.Paid)
        {
            throw new BusinessRuleException(
                "ORDER_PAYMENT_NOT_CONFIRMED",
                "Payment must be confirmed before the vendor can process this order.");
        }
    }
}
