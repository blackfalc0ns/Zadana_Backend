using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Payments.Commands.RetryPaymobPayment;

public record RetryPaymobPaymentCommand(Guid OrderId, Guid UserId) : IRequest<RetryPaymobPaymentResultDto>;

public record RetryPaymobPaymentResultDto(string Message, CheckoutPaymentSessionDto Payment);

public class RetryPaymobPaymentCommandValidator : AbstractValidator<RetryPaymobPaymentCommand>
{
    public RetryPaymobPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class RetryPaymobPaymentCommandHandler : IRequestHandler<RetryPaymobPaymentCommand, RetryPaymobPaymentResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymobGateway _paymobGateway;
    private readonly IUnitOfWork _unitOfWork;

    public RetryPaymobPaymentCommandHandler(
        IApplicationDbContext context,
        IPaymobGateway paymobGateway,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _paymobGateway = paymobGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<RetryPaymobPaymentResultDto> Handle(RetryPaymobPaymentCommand request, CancellationToken cancellationToken)
    {
        if (!_paymobGateway.IsEnabled)
        {
            throw new BusinessRuleException("PAYMENT_UNAVAILABLE", "Paymob checkout is disabled or not configured.");
        }

        var order = await _context.Orders
            .AsTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == request.OrderId && x.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        if (order.PaymentMethod != PaymentMethodType.Card || order.Status != OrderStatus.PendingPayment)
        {
            throw new BusinessRuleException(
                "ORDER_PAYMENT_RETRY_NOT_ALLOWED",
                "Payment retry is only allowed for card orders awaiting payment confirmation.");
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            throw new BusinessRuleException("ORDER_ALREADY_PAID", "Order payment is already confirmed.");
        }

        var latestPayment = await _context.Payments
            .Include(x => x.Order)
            .Where(x => x.OrderId == order.Id && x.Method == PaymentMethodType.Card)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestPayment == null)
        {
            throw new NotFoundException("Payment", order.Id);
        }

        if (latestPayment.Status == PaymentStatus.Paid)
        {
            throw new BusinessRuleException("ORDER_ALREADY_PAID", "Order payment is already confirmed.");
        }

        if (latestPayment.Status is not (PaymentStatus.Initiated or PaymentStatus.Pending or PaymentStatus.Failed))
        {
            throw new BusinessRuleException(
                "ORDER_PAYMENT_RETRY_NOT_ALLOWED",
                $"Cannot retry payment from status {latestPayment.Status}.");
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        var address = await _context.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == order.CustomerAddressId && x.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("CustomerAddress", order.CustomerAddressId);

        if (latestPayment.Status is PaymentStatus.Initiated or PaymentStatus.Pending)
        {
            latestPayment.MarkAsFailed("Payment attempt superseded by retry.");
        }

        var retryPayment = new Payment(order.Id, PaymentMethodType.Card, order.TotalAmount);
        _context.Payments.Add(retryPayment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var session = await _paymobGateway.CreateCheckoutSessionAsync(
                new Zadana.Application.Modules.Payments.DTOs.PaymobCheckoutSessionRequest(
                    retryPayment.Id,
                    order.Id,
                    order.OrderNumber,
                    order.TotalAmount,
                    CheckoutSupport.Currency,
                    order.Items.Select(MapPaymobItem).ToArray(),
                    GetFirstName(user.FullName),
                    GetLastName(user.FullName),
                    user.Email ?? string.Empty,
                    user.PhoneNumber ?? address.ContactPhone,
                    address.AddressLine,
                    address.City ?? address.Area ?? "Cairo",
                    "EG"),
                cancellationToken);

            retryPayment.MarkAsPending("Paymob", session.ProviderReference);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new RetryPaymobPaymentResultDto(
                "payment retry session created successfully",
                new CheckoutPaymentSessionDto(
                    retryPayment.Id,
                    "paymob",
                    CheckoutSupport.MapPaymentStatusToContractValue(retryPayment.Status.ToString()),
                    session.IframeUrl,
                    session.ProviderReference));
        }
        catch
        {
            retryPayment.MarkAsFailed("Paymob checkout retry session creation failed.");
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static string GetFirstName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.FirstOrDefault() ?? "Customer";
    }

    private static string GetLastName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : "Customer";
    }

    private static Zadana.Application.Modules.Payments.DTOs.PaymobOrderItemRequest MapPaymobItem(Zadana.Domain.Modules.Orders.Entities.OrderItem item) =>
        new(
            item.ProductName,
            item.ProductName,
            item.Quantity,
            item.UnitPrice);
}
