using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Payments.Commands.ConfirmCardPayment;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Payments.Commands.ProcessPaymentWebhook;

/// <summary>
/// Persists an inbound payment provider webhook into the durable
/// <c>PaymentProviderEventInbox</c> (idempotent on Provider+EventId) and
/// triggers verification through <see cref="ConfirmCardPaymentCommand"/>.
/// </summary>
public record ProcessPaymentWebhookCommand(
    string Provider,
    string Payload,
    bool SecretValid,
    string? Headers = null) : IRequest<PaymentWebhookProcessResultDto>;

public class ProcessPaymentWebhookCommandValidator : AbstractValidator<ProcessPaymentWebhookCommand>
{
    public ProcessPaymentWebhookCommandValidator()
    {
        RuleFor(x => x.Provider).NotEmpty();
        RuleFor(x => x.Payload).NotEmpty();
    }
}

public class ProcessPaymentWebhookCommandHandler : IRequestHandler<ProcessPaymentWebhookCommand, PaymentWebhookProcessResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public ProcessPaymentWebhookCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<PaymentWebhookProcessResultDto> Handle(ProcessPaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        var (eventId, eventType, providerPaymentId) = ParseEnvelope(request.Provider, request.Payload);

        var existing = await _context.PaymentProviderEvents
            .FirstOrDefaultAsync(x => x.ProviderName == request.Provider && x.ProviderEventId == eventId, cancellationToken);

        PaymentProviderEventInbox inbox;
        if (existing is null)
        {
            inbox = new PaymentProviderEventInbox(
                providerName: request.Provider,
                providerEventId: eventId,
                eventType: eventType,
                rawPayload: request.Payload,
                secretValid: request.SecretValid,
                providerPaymentId: providerPaymentId,
                headers: request.Headers);
            _context.PaymentProviderEvents.Add(inbox);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                inbox = await _context.PaymentProviderEvents
                    .FirstAsync(x => x.ProviderName == request.Provider && x.ProviderEventId == eventId, cancellationToken);
            }
        }
        else
        {
            inbox = existing;
        }

        if (!request.SecretValid)
        {
            inbox.MarkFailed("Webhook signature was not validated.");
            await _context.SaveChangesAsync(cancellationToken);
            throw new BusinessRuleException("PAYMENT_WEBHOOK_INVALID_SIGNATURE", "Webhook signature could not be validated.");
        }

        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            inbox.MarkIgnored("Payload does not reference a provider payment id.");
            await _context.SaveChangesAsync(cancellationToken);
            return new PaymentWebhookProcessResultDto("ignored", Guid.Empty, "ignored");
        }

        inbox.MarkProcessing();
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await _sender.Send(
                new ConfirmCardPaymentCommand(
                    PaymentId: null,
                    ProviderPaymentId: providerPaymentId,
                    ProviderName: request.Provider,
                    CustomerDeviceId: null),
                cancellationToken);

            inbox.MarkProcessed();
            await _context.SaveChangesAsync(cancellationToken);

            return new PaymentWebhookProcessResultDto(result.Message, result.PaymentId, result.PaymentStatus);
        }
        catch (Exception ex)
        {
            inbox.MarkFailed(ex.Message);
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static (string EventId, string EventType, string? ProviderPaymentId) ParseEnvelope(string provider, string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var eventId = TryRead(root, "id") ?? TryRead(root, "data", "id") ?? Guid.NewGuid().ToString();
            var eventType = TryRead(root, "type") ?? TryRead(root, "event") ?? "unknown";
            var providerPaymentId = TryRead(root, "data", "id") ?? TryRead(root, "id");
            return (eventId, eventType, providerPaymentId);
        }
        catch (JsonException)
        {
            // Fall back to a hash-based id when the body is not JSON; we still want to retain it for audit.
            var fallback = $"{provider}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)))}";
            return (fallback, "unparseable", null);
        }
    }

    private static string? TryRead(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object) return null;
            if (!current.TryGetProperty(segment, out var next)) return null;
            current = next;
        }
        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            _ => null,
        };
    }
}
