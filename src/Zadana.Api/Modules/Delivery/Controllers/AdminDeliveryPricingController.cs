using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Api.Controllers;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.Modules.Delivery.Controllers;

[Route("api/admin/delivery-pricing")]
[Authorize(Policy = "AdminOnly")]
[Tags("Admin Dashboard API")]
public class AdminDeliveryPricingController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeliveryPricingRuleDto>>> GetRules(
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var rules = await context.DeliveryPricingRules
            .Include(item => item.SurgeWindows)
            .OrderBy(item => item.City)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return Ok(rules.Select(MapRule).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<DeliveryPricingRuleDto>> CreateRule(
        [FromBody] UpsertDeliveryPricingRuleRequest request,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var rule = new DeliveryPricingRule(
            request.DeliveryZoneId,
            request.City,
            request.Name,
            request.BaseFee,
            request.IncludedKm,
            request.PerKmFee,
            request.MinFee,
            request.MaxFee,
            request.IsActive);

        foreach (var window in request.SurgeWindows ?? [])
        {
            rule.SurgeWindows.Add(new DeliveryPricingSurgeWindow(
                rule.Id,
                window.Name,
                ParseTime(window.StartLocalTime),
                ParseTime(window.EndLocalTime),
                window.Multiplier,
                window.IsActive));
        }

        context.DeliveryPricingRules.Add(rule);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(MapRule(rule));
    }

    [HttpPut("{ruleId:guid}")]
    public async Task<ActionResult<DeliveryPricingRuleDto>> UpdateRule(
        Guid ruleId,
        [FromBody] UpsertDeliveryPricingRuleRequest request,
        [FromServices] IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var rule = await context.DeliveryPricingRules
            .Include(item => item.SurgeWindows)
            .FirstOrDefaultAsync(item => item.Id == ruleId, cancellationToken)
            ?? throw new NotFoundException("DeliveryPricingRule", ruleId);

        rule.Update(
            request.DeliveryZoneId,
            request.City,
            request.Name,
            request.BaseFee,
            request.IncludedKm,
            request.PerKmFee,
            request.MinFee,
            request.MaxFee,
            request.IsActive);

        var existingWindows = rule.SurgeWindows.ToList();
        context.DeliveryPricingSurgeWindows.RemoveRange(existingWindows);
        rule.SurgeWindows.Clear();

        foreach (var window in request.SurgeWindows ?? [])
        {
            rule.SurgeWindows.Add(new DeliveryPricingSurgeWindow(
                rule.Id,
                window.Name,
                ParseTime(window.StartLocalTime),
                ParseTime(window.EndLocalTime),
                window.Multiplier,
                window.IsActive));
        }

        await context.SaveChangesAsync(cancellationToken);
        return Ok(MapRule(rule));
    }

    private static TimeSpan ParseTime(string value)
    {
        if (!TimeSpan.TryParse(value, out var parsed))
        {
            throw new BusinessRuleException("INVALID_TIME_FORMAT", "Time must be in HH:mm or HH:mm:ss format.");
        }

        return parsed;
    }

    private static void ValidateRequest(UpsertDeliveryPricingRuleRequest request)
    {
        if (request.BaseFee < 0m ||
            request.IncludedKm < 0m ||
            request.PerKmFee < 0m ||
            request.MinFee < 0m ||
            request.MaxFee < 0m)
        {
            throw new BusinessRuleException("INVALID_DELIVERY_PRICING", "Delivery pricing amounts cannot be negative.");
        }

        if (request.MaxFee > 0m && request.MaxFee < request.MinFee)
        {
            throw new BusinessRuleException("INVALID_DELIVERY_PRICING", "MaxFee must be zero or greater than or equal to MinFee.");
        }

        foreach (var window in request.SurgeWindows ?? [])
        {
            if (window.Multiplier < 1m)
            {
                throw new BusinessRuleException("INVALID_SURGE_MULTIPLIER", "Surge multiplier must be greater than or equal to 1.");
            }

            _ = ParseTime(window.StartLocalTime);
            _ = ParseTime(window.EndLocalTime);
        }
    }

    private static DeliveryPricingRuleDto MapRule(DeliveryPricingRule rule) =>
        new(
            rule.Id,
            rule.DeliveryZoneId,
            rule.City,
            rule.Name,
            rule.BaseFee,
            rule.IncludedKm,
            rule.PerKmFee,
            rule.MinFee,
            rule.MaxFee,
            rule.IsActive,
            rule.SurgeWindows
                .OrderBy(item => item.StartLocalTime)
                .Select(item => new DeliveryPricingSurgeWindowDto(
                    item.Id,
                    item.Name,
                    item.StartLocalTime,
                    item.EndLocalTime,
                    item.Multiplier,
                    item.IsActive))
                .ToArray());
}

public record UpsertDeliveryPricingRuleRequest(
    Guid? DeliveryZoneId,
    string City,
    string Name,
    decimal BaseFee,
    decimal IncludedKm,
    decimal PerKmFee,
    decimal MinFee,
    decimal MaxFee,
    bool IsActive,
    DeliveryPricingSurgeWindowRequest[]? SurgeWindows);

public record DeliveryPricingSurgeWindowRequest(
    string Name,
    string StartLocalTime,
    string EndLocalTime,
    decimal Multiplier,
    bool IsActive);
