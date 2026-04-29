using FluentValidation;
using MediatR;
using Zadana.Application.Modules.Payments.Commands.ConfirmPaymobPayment;
using Zadana.Application.Modules.Payments.DTOs;

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
    private readonly ISender _sender;

    public ProcessPaymobWebhookCommandHandler(
        ISender sender)
    {
        _sender = sender;
    }

    public async Task<PaymobWebhookProcessResultDto> Handle(ProcessPaymobWebhookCommand request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ConfirmPaymobPaymentCommand(
                null,
                request.Payload,
                null,
                null,
                null,
                null),
            cancellationToken);

        return new PaymobWebhookProcessResultDto(
            result.MessageAr,
            result.MessageEn,
            result.PaymentId,
            result.PaymentStatus);
    }
}
