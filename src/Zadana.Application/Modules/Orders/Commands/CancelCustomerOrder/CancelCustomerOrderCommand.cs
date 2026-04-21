using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.CancelCustomerOrder;

public record CancelCustomerOrderCommand(
    Guid OrderId,
    Guid UserId,
    string Reason,
    string? Note) : IRequest<CancelCustomerOrderResultDto>;

public record CancelCustomerOrderResultDto(Guid Id, string Status, string Message);

public class CancelCustomerOrderCommandValidator : AbstractValidator<CancelCustomerOrderCommand>
{
    public CancelCustomerOrderCommandValidator(IStringLocalizer<SharedResource> localizer)
    {
        var reasonRequired = localizer["RequiredField"].Value.Replace("{PropertyName}", "reason");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage(reasonRequired)
            .MaximumLength(500);

        RuleFor(x => x.Note)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Note));
    }
}

public class CancelCustomerOrderCommandHandler : IRequestHandler<CancelCustomerOrderCommand, CancelCustomerOrderResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public CancelCustomerOrderCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork, IPublisher publisher)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<CancelCustomerOrderResultDto> Handle(CancelCustomerOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == request.OrderId && x.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        if (!CanCancel(order.Status))
        {
            throw new BusinessRuleException("ORDER_CANNOT_BE_CANCELLED", "Order cannot be cancelled at the current stage.");
        }

        var oldStatus = order.Status;
        var note = string.IsNullOrWhiteSpace(request.Note)
            ? $"Customer cancellation reason: {request.Reason.Trim()}"
            : $"Customer cancellation reason: {request.Reason.Trim()}. Note: {request.Note.Trim()}";

        order.ChangeStatus(OrderStatus.Cancelled, null, note);
        _context.OrderStatusHistories.Add(order.StatusHistory.Last());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish notification event
        await _publisher.Publish(
            new OrderStatusChangedNotification(
                order.Id,
                order.UserId,
                order.VendorId,
                order.OrderNumber,
                oldStatus,
                OrderStatus.Cancelled,
                NotifyCustomer: true,
                NotifyVendor: true,
                ActorRole: "customer"),
            cancellationToken);

        return new CancelCustomerOrderResultDto(order.Id, "cancelled", "order cancelled successfully");
    }

    private static bool CanCancel(OrderStatus status) =>
        status is OrderStatus.PendingVendorAcceptance or
            OrderStatus.Accepted or
            OrderStatus.Preparing;
}
