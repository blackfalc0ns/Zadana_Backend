using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Payments.Commands.ProcessPaymobWebhook;

public record ProcessPaymobWebhookCommand(string Payload) : IRequest<PaymobWebhookProcessResultDto>;

public class ProcessPaymobWebhookCommandValidator : AbstractValidator<ProcessPaymobWebhookCommand>
{
    public ProcessPaymobWebhookCommandValidator()
    {
        RuleFor(x => x.Payload).NotEmpty();
    }
}

public class ProcessPaymobWebhookCommandHandler : IRequestHandler<ProcessPaymobWebhookCommand, PaymobWebhookProcessResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymobGateway _paymobGateway;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessPaymobWebhookCommandHandler(
        IApplicationDbContext context,
        IPaymobGateway paymobGateway,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _paymobGateway = paymobGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<PaymobWebhookProcessResultDto> Handle(ProcessPaymobWebhookCommand request, CancellationToken cancellationToken)
    {
        var notification = _paymobGateway.ParseWebhookNotification(request.Payload);

        var payment = notification.PaymentId.HasValue
            ? await _context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(x => x.Id == notification.PaymentId.Value, cancellationToken)
            : await _context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(
                    x => x.ProviderName == "Paymob" && x.ProviderTransactionId == notification.ProviderReference,
                    cancellationToken);

        if (payment == null)
        {
            var lookupId = notification.PaymentId?.ToString() ?? notification.ProviderReference ?? "unknown";
            throw new NotFoundException("Payment", lookupId);
        }

        if (!string.IsNullOrWhiteSpace(notification.ProviderReference) &&
            !string.Equals(payment.ProviderTransactionId, notification.ProviderReference, StringComparison.Ordinal))
        {
            payment.SetProviderTransactionId(notification.ProviderReference);
        }

        if (notification.IsSuccess)
        {
            if (payment.Status != PaymentStatus.Paid)
            {
                payment.MarkAsPaid(notification.ProviderTransactionId);
                await ClearCustomerCartAsync(payment.Order.UserId, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new PaymobWebhookProcessResultDto("Payment marked as paid.", payment.Id, ToApiToken(payment.Status.ToString()));
        }

        if (!notification.IsPending && payment.Status != PaymentStatus.Paid && payment.Status != PaymentStatus.Failed)
        {
            payment.MarkAsFailed("Paymob reported payment failure.", notification.ProviderTransactionId);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new PaymobWebhookProcessResultDto("Webhook processed successfully.", payment.Id, ToApiToken(payment.Status.ToString()));
    }

    private async Task ClearCustomerCartAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (cart != null)
        {
            _context.Carts.Remove(cart);
        }
    }

    private static string ToApiToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0 && char.IsUpper(character))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
