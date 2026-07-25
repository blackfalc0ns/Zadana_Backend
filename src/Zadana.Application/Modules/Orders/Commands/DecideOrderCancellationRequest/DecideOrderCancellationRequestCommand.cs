using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

namespace Zadana.Application.Modules.Orders.Commands.DecideOrderCancellationRequest;

public record DecideOrderCancellationRequestCommand(
    Guid OrderId,
    Guid VendorId,
    Guid? BranchId,
    Guid VendorUserId,
    Guid CancellationRequestId,
    bool Accept,
    string? Note) : IRequest<DecideOrderCancellationRequestResultDto>;

public record DecideOrderCancellationRequestResultDto(
    Guid OrderId,
    Guid CancellationRequestId,
    string RequestStatus,
    string OrderStatus,
    string Message);

public class DecideOrderCancellationRequestCommandValidator : AbstractValidator<DecideOrderCancellationRequestCommand>
{
    public DecideOrderCancellationRequestCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.VendorUserId).NotEmpty();
        RuleFor(x => x.CancellationRequestId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.Note));
    }
}

public class DecideOrderCancellationRequestCommandHandler
    : IRequestHandler<DecideOrderCancellationRequestCommand, DecideOrderCancellationRequestResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly IPaymentGatewayResolver _gatewayResolver;
    private readonly OrderInventoryWorkflowService _orderInventoryWorkflowService;
    private readonly ILogger<DecideOrderCancellationRequestCommandHandler> _logger;

    public DecideOrderCancellationRequestCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        IPaymentGatewayResolver gatewayResolver,
        OrderInventoryWorkflowService orderInventoryWorkflowService,
        ILogger<DecideOrderCancellationRequestCommandHandler> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _gatewayResolver = gatewayResolver;
        _orderInventoryWorkflowService = orderInventoryWorkflowService;
        _logger = logger;
    }

    public async Task<DecideOrderCancellationRequestResultDto> Handle(
        DecideOrderCancellationRequestCommand request,
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

        var cancellationRequest = await _context.OrderCancellationRequests
            .FirstOrDefaultAsync(item =>
                item.Id == request.CancellationRequestId &&
                item.OrderId == order.Id,
                cancellationToken)
            ?? throw new NotFoundException("OrderCancellationRequest", request.CancellationRequestId);

        if (cancellationRequest.Status != OrderCancellationRequestStatus.Pending)
        {
            throw new BusinessRuleException(
                "CANCELLATION_REQUEST_ALREADY_DECIDED",
                "Cancellation request has already been decided.");
        }

        if (!request.Accept)
        {
            cancellationRequest.Reject(request.VendorUserId, request.Note);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new DecideOrderCancellationRequestResultDto(
                order.Id,
                cancellationRequest.Id,
                "rejected",
                order.Status.ToString(),
                "Cancellation request rejected.");
        }

        var oldStatus = order.Status;
        cancellationRequest.Accept(request.VendorUserId, request.Note);
        order.ChangeStatus(
            OrderStatus.Cancelled,
            request.VendorUserId,
            request.Note ?? "Vendor accepted customer cancellation request.");
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());
        await _orderInventoryWorkflowService.ApplyRestockAsync(order.Id, "pickup_cancellation_accepted", cancellationToken);

        await OrderCancellationRefundSupport.TryRefundPaidOrderAsync(
            _context,
            _gatewayResolver,
            _logger,
            order,
            "Pickup cancellation accepted by vendor.",
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                OrderStatus.Cancelled,
                NotifyCustomer: true,
                NotifyVendor: false,
                ActorRole: "vendor"),
            cancellationToken);

        return new DecideOrderCancellationRequestResultDto(
            order.Id,
            cancellationRequest.Id,
            "accepted",
            "cancelled",
            LocalizedMessages.GetCurrent(LocalizedMessages.OrderCancelledSuccess));
    }
}
