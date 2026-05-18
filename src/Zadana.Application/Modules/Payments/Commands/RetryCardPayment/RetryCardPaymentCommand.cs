using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Support;
using Zadana.Application.Modules.Payments.Gateways;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Payments.Support;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;
using Zadana.SharedKernel.Finance;

namespace Zadana.Application.Modules.Payments.Commands.RetryCardPayment;

public record RetryCardPaymentCommand(Guid OrderId, Guid UserId) : IRequest<RetryCardPaymentResultDto>;

public record RetryCardPaymentResultDto(string Message, CheckoutPaymentSessionDto Payment);

public class RetryCardPaymentCommandValidator : AbstractValidator<RetryCardPaymentCommand>
{
    public RetryCardPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class RetryCardPaymentCommandHandler : IRequestHandler<RetryCardPaymentCommand, RetryCardPaymentResultDto>
{
    private const string ProviderName = "Moyasar";

    private readonly IApplicationDbContext _context;
    private readonly IPaymentGatewayResolver _gatewayResolver;
    private readonly IUnitOfWork _unitOfWork;

    public RetryCardPaymentCommandHandler(
        IApplicationDbContext context,
        IPaymentGatewayResolver gatewayResolver,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _gatewayResolver = gatewayResolver;
        _unitOfWork = unitOfWork;
    }

    public async Task<RetryCardPaymentResultDto> Handle(RetryCardPaymentCommand request, CancellationToken cancellationToken)
    {
        if (!_gatewayResolver.TryResolve(ProviderName, out var gateway) || gateway is null)
        {
            throw new BusinessRuleException("PAYMENT_UNAVAILABLE", "Card checkout provider is disabled or not configured.");
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

        CurrencyPolicy.EnsureOfficial(order.Currency);

        var latestPayment = await _context.Payments
            .Include(x => x.Order)
            .Where(x => x.OrderId == order.Id && x.Method == PaymentMethodType.Card)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Payment", order.Id);

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

        if (latestPayment.Status is PaymentStatus.Initiated or PaymentStatus.Pending)
        {
            latestPayment.MarkAsFailed("Payment attempt superseded by retry.");
        }

        var retryPayment = new Payment(order.Id, PaymentMethodType.Card, order.TotalAmount);
        _context.Payments.Add(retryPayment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var idempotencyKey = $"payment-create:{order.Id:N}:{retryPayment.Id:N}";
            var session = await gateway.CreateSessionAsync(
                new CreatePaymentSessionCommand(
                    OrderId: order.Id,
                    PaymentId: retryPayment.Id,
                    Channel: PaymentMethodChannel.Card,
                    Amount: order.TotalAmount,
                    Currency: order.Currency,
                    Description: $"Order {order.OrderNumber}",
                    CallbackUrl: string.Empty,
                    IdempotencyKey: idempotencyKey,
                    Metadata: new Dictionary<string, string>
                    {
                        ["order_id"] = order.Id.ToString(),
                        ["payment_id"] = retryPayment.Id.ToString(),
                        ["order_number"] = order.OrderNumber,
                        ["retry_of"] = latestPayment.Id.ToString(),
                    },
                    CustomerEmail: user.Email,
                    CustomerPhone: user.PhoneNumber,
                    CustomerFullName: user.FullName),
                cancellationToken);

            retryPayment.ApplyProviderSession(
                providerName: session.ProviderName,
                providerMethod: "creditcard",
                providerPaymentId: session.ProviderPaymentId,
                providerInvoiceId: session.ProviderInvoiceId,
                idempotencyKey: idempotencyKey,
                rawCreateResponse: session.RawCreateResponse,
                currency: order.Currency);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new RetryCardPaymentResultDto(
                "payment retry session created successfully",
                new CheckoutPaymentSessionDto(
                    retryPayment.Id,
                    session.ProviderName.ToLowerInvariant(),
                    CheckoutSupport.MapPaymentStatusToContractValue(retryPayment.Status.ToString()),
                    BuildClientHint(session),
                    session.ProviderPaymentId ?? string.Empty));
        }
        catch
        {
            await UnconfirmedCardPaymentCleanup.DeletePaymentAsync(_context, retryPayment.Id, cancellationToken);
            throw;
        }
    }

    private static string BuildClientHint(CreatePaymentSessionResult session)
    {
        // Customer apps render the Moyasar form using ProviderConfig (publishable key, amount, etc.).
        // The legacy contract still ships an "iframe url"-shaped string; expose the provider name for now.
        return session.ClientAction;
    }
}
