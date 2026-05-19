using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Domain.Modules.Payments.Entities;

namespace Zadana.Application.Modules.Payments.Commands.ProcessPaymentWebhook;

/// <summary>
/// Persists an inbound payment provider webhook into the durable
/// <c>PaymentProviderEventInbox</c> (idempotent on Provider+EventId).
/// A background worker performs the actual provider fetch and verification.
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

    public ProcessPaymentWebhookCommandHandler(IApplicationDbContext context)
    {
        _context = context;
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
            // Record the audit trail. The HTTP-layer controller is responsible for
            // returning the right status (401) to the caller; this command must not
            // throw because the caller is reporting on a verified-failure path that
            // is allowed and expected (signature mismatch, missing config, replay).
            inbox.MarkFailed("Webhook signature was not validated.");
            await _context.SaveChangesAsync(cancellationToken);
            return new PaymentWebhookProcessResultDto(
                "signature_invalid",
                Guid.Empty,
                "rejected");
        }

        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            inbox.MarkIgnored("Payload does not reference a provider payment id.");
            await _context.SaveChangesAsync(cancellationToken);
            return new PaymentWebhookProcessResultDto("ignored", Guid.Empty, "ignored");
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new PaymentWebhookProcessResultDto(
            "queued",
            Guid.Empty,
            inbox.Status.ToString().ToLowerInvariant(),
            providerPaymentId);
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
