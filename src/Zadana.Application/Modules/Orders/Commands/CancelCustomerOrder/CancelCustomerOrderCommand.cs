using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.CancelCustomerOrder;

public record CancelCustomerOrderCommand(
    Guid OrderId,
    Guid UserId,
    string? ReasonCode,
    string? Reason,
    string? Note) : IRequest<CancelCustomerOrderResultDto>;

public record CancelCustomerOrderResultDto(Guid Id, string Status, string Message);

public class CancelCustomerOrderCommandValidator : AbstractValidator<CancelCustomerOrderCommand>
{
    public CancelCustomerOrderCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        var reasonRequired = localizer["RequiredField"].Value.Replace("{PropertyName}", "reason");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.ReasonCode) || !string.IsNullOrWhiteSpace(x.Reason))
            .WithMessage(reasonRequired);

        RuleFor(x => x.ReasonCode)
            .Must(code => string.IsNullOrWhiteSpace(code) || CustomerOrderCancellationReasonCatalog.IsValidCode(code))
            .WithMessage("Reason code is invalid.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason));

        RuleFor(x => x.Note)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Note));

        RuleFor(x => x.Note)
            .NotEmpty()
            .When(x => string.Equals(x.ReasonCode, "other", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Note is required when reason code is other.");
    }
}

public class CancelCustomerOrderCommandHandler : IRequestHandler<CancelCustomerOrderCommand, CancelCustomerOrderResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly IPaymentGatewayResolver? _gatewayResolver;
    private readonly OrderInventoryWorkflowService _orderInventoryWorkflowService;
    private readonly ILogger<CancelCustomerOrderCommandHandler> _logger;

    public CancelCustomerOrderCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        IPaymentGatewayResolver? gatewayResolver = null,
        OrderInventoryWorkflowService? orderInventoryWorkflowService = null,
        ILogger<CancelCustomerOrderCommandHandler>? logger = null)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _gatewayResolver = gatewayResolver;
        _orderInventoryWorkflowService = orderInventoryWorkflowService ?? new OrderInventoryWorkflowService(context);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CancelCustomerOrderCommandHandler>.Instance;
    }

    public async Task<CancelCustomerOrderResultDto> Handle(CancelCustomerOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == request.OrderId && x.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        var resolvedReason = ResolveReasonText(request);
        var note = string.IsNullOrWhiteSpace(request.Note)
            ? $"Customer cancellation reason: {resolvedReason}"
            : $"Customer cancellation reason: {resolvedReason}. Note: {request.Note.Trim()}";

        if (order.IsPickup)
        {
            return await HandlePickupCancellationAsync(order, note, cancellationToken);
        }

        if (!CanCancelDelivery(order.Status))
        {
            throw new BusinessRuleException("ORDER_CANNOT_BE_CANCELLED", "Order cannot be cancelled at the current stage.");
        }

        return await CancelImmediatelyAsync(order, note, cancellationToken);
    }

    private async Task<CancelCustomerOrderResultDto> HandlePickupCancellationAsync(
        Order order,
        string note,
        CancellationToken cancellationToken)
    {
        if (CanCancelPickupImmediately(order.Status))
        {
            return await CancelImmediatelyAsync(order, note, cancellationToken, refundIfPaid: true);
        }

        if (RequiresPickupCancellationApproval(order.Status))
        {
            return await CreatePickupCancellationRequestAsync(order, note, cancellationToken);
        }

        throw new BusinessRuleException("ORDER_CANNOT_BE_CANCELLED", "Order cannot be cancelled at the current stage.");
    }

    private async Task<CancelCustomerOrderResultDto> CreatePickupCancellationRequestAsync(
        Order order,
        string note,
        CancellationToken cancellationToken)
    {
        var hasPendingRequest = await _context.OrderCancellationRequests
            .AnyAsync(item =>
                item.OrderId == order.Id &&
                item.Status == OrderCancellationRequestStatus.Pending,
                cancellationToken);

        if (hasPendingRequest)
        {
            return new CancelCustomerOrderResultDto(
                order.Id,
                "cancellation_requested",
                "A cancellation request is already pending vendor approval.");
        }

        var cancellationRequest = new OrderCancellationRequest(order.Id, order.UserId, note);
        _context.OrderCancellationRequests.Add(cancellationRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CancelCustomerOrderResultDto(
            order.Id,
            "cancellation_requested",
            "Cancellation request submitted and is awaiting vendor approval.");
    }

    private async Task<CancelCustomerOrderResultDto> CancelImmediatelyAsync(
        Order order,
        string note,
        CancellationToken cancellationToken,
        bool refundIfPaid = false)
    {
        var oldStatus = order.Status;
        order.ChangeStatus(OrderStatus.Cancelled, null, note);
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());
        await _orderInventoryWorkflowService.ApplyRestockAsync(order.Id, "customer_cancelled", cancellationToken);

        if (refundIfPaid)
        {
            await OrderCancellationRefundSupport.TryRefundPaidOrderAsync(
                _context,
                _gatewayResolver,
                _logger,
                order,
                "Customer cancelled pickup order before vendor preparation.",
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                OrderStatus.Cancelled,
                // Customer initiated the cancel — do not spam them with "order cancelled" again.
                NotifyCustomer: false,
                NotifyVendor: true,
                ActorRole: "customer"),
            cancellationToken);

        return new CancelCustomerOrderResultDto(
            order.Id,
            "cancelled",
            LocalizedMessages.GetCurrent(LocalizedMessages.OrderCancelledSuccess));
    }

    private static string ResolveReasonText(CancelCustomerOrderCommand request)
    {
        var option = CustomerOrderCancellationReasonCatalog.FindByCode(request.ReasonCode);
        if (option != null)
        {
            return $"{option.Code}: {option.LabelEn}";
        }

        return request.Reason!.Trim();
    }

    private static bool CanCancelDelivery(OrderStatus status) =>
        status is OrderStatus.PendingVendorAcceptance or
            OrderStatus.Accepted or
            OrderStatus.Preparing;

    private static bool CanCancelPickupImmediately(OrderStatus status) =>
        status is OrderStatus.PendingPayment or
            OrderStatus.Placed or
            OrderStatus.PendingVendorAcceptance;

    private static bool RequiresPickupCancellationApproval(OrderStatus status) =>
        status is OrderStatus.Accepted or
            OrderStatus.Preparing or
            OrderStatus.ReadyForPickup;
}
