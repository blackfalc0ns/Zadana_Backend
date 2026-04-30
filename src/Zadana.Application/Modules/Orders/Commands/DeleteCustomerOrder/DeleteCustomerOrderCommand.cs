using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Orders.Commands.DeleteCustomerOrder;

public record DeleteCustomerOrderCommand(Guid OrderId, Guid UserId) : IRequest<DeleteCustomerOrderResultDto>;

public record DeleteCustomerOrderResultDto(Guid OrderId, string Message);

public class DeleteCustomerOrderCommandValidator : AbstractValidator<DeleteCustomerOrderCommand>
{
    public DeleteCustomerOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class DeleteCustomerOrderCommandHandler : IRequestHandler<DeleteCustomerOrderCommand, DeleteCustomerOrderResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCustomerOrderCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteCustomerOrderResultDto> Handle(DeleteCustomerOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == request.OrderId && x.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        if (order.Status != OrderStatus.PendingPayment || order.PaymentStatus == PaymentStatus.Paid)
        {
            throw new BusinessRuleException(
                "ORDER_DELETE_NOT_ALLOWED",
                "Only unpaid pending-payment orders can be deleted.");
        }

        var payments = await _context.Payments
            .Include(x => x.Refunds)
            .Where(x => x.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        if (payments.Any(payment => payment.Status == PaymentStatus.Paid))
        {
            throw new BusinessRuleException("ORDER_ALREADY_PAID", "Paid orders cannot be deleted.");
        }

        var complaints = await _context.OrderComplaints
            .Include(x => x.Attachments)
            .Where(x => x.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        var supportCases = await _context.OrderSupportCases
            .Include(x => x.Attachments)
            .Include(x => x.Activities)
            .Where(x => x.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        var attachments = complaints.SelectMany(x => x.Attachments).ToList();
        var supportCaseAttachments = supportCases.SelectMany(x => x.Attachments).ToList();
        var supportCaseActivities = supportCases.SelectMany(x => x.Activities).ToList();
        var refunds = payments.SelectMany(x => x.Refunds).ToList();
        var statusHistory = await _context.OrderStatusHistories.Where(x => x.OrderId == order.Id).ToListAsync(cancellationToken);
        var items = await _context.OrderItems.Where(x => x.OrderId == order.Id).ToListAsync(cancellationToken);

        if (attachments.Count > 0)
        {
            _context.OrderComplaintAttachments.RemoveRange(attachments);
        }

        if (supportCaseAttachments.Count > 0)
        {
            _context.OrderSupportCaseAttachments.RemoveRange(supportCaseAttachments);
        }

        if (supportCaseActivities.Count > 0)
        {
            _context.OrderSupportCaseActivities.RemoveRange(supportCaseActivities);
        }

        if (complaints.Count > 0)
        {
            _context.OrderComplaints.RemoveRange(complaints);
        }

        if (supportCases.Count > 0)
        {
            _context.OrderSupportCases.RemoveRange(supportCases);
        }

        if (refunds.Count > 0)
        {
            _context.Refunds.RemoveRange(refunds);
        }

        if (payments.Count > 0)
        {
            _context.Payments.RemoveRange(payments);
        }

        if (statusHistory.Count > 0)
        {
            _context.OrderStatusHistories.RemoveRange(statusHistory);
        }

        if (items.Count > 0)
        {
            _context.OrderItems.RemoveRange(items);
        }

        _context.Orders.Remove(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeleteCustomerOrderResultDto(order.Id, "order deleted successfully");
    }
}
