using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Finances.Services;

namespace Zadana.Api.Modules.Finances.Controllers;

[ApiController]
[Route("api/admin/payouts")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminPayoutsController(
    IApplicationDbContext context,
    PayoutOrchestrator payoutOrchestrator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminPayoutListDto>> GetPayouts(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = context.Payouts
            .AsNoTracking()
            .Include(item => item.Settlement)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status.ToString() == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AdminPayoutDto(
                item.Id,
                item.SettlementId,
                item.Settlement.OwnerType.ToString(),
                item.Settlement.OwnerId,
                item.Amount,
                item.Status.ToString(),
                item.ProviderName,
                item.ProviderTransferId,
                item.TransferReference,
                item.FailureReason,
                item.TriggeredAtUtc,
                item.CompletedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new AdminPayoutListDto(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminPayoutDetailDto>> GetPayout(Guid id, CancellationToken cancellationToken)
    {
        var payout = await context.Payouts
            .AsNoTracking()
            .Include(item => item.Settlement)
            .Include(item => item.Attempts)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (payout is null)
        {
            return NotFound();
        }

        return Ok(new AdminPayoutDetailDto(
            new AdminPayoutDto(
                payout.Id,
                payout.SettlementId,
                payout.Settlement.OwnerType.ToString(),
                payout.Settlement.OwnerId,
                payout.Amount,
                payout.Status.ToString(),
                payout.ProviderName,
                payout.ProviderTransferId,
                payout.TransferReference,
                payout.FailureReason,
                payout.TriggeredAtUtc,
                payout.CompletedAtUtc),
            payout.Attempts
                .OrderByDescending(item => item.CreatedAtUtc)
                .Select(item => new AdminPayoutAttemptDto(
                    item.Id,
                    item.AttemptType.ToString(),
                    item.Status.ToString(),
                    item.ProviderTransferId,
                    item.TransferReference,
                    item.FailureReason,
                    item.CreatedAtUtc))
                .ToList()));
    }

    [HttpPost("{id:guid}/trigger")]
    public async Task<ActionResult<AdminPayoutDto>> Trigger(Guid id, CancellationToken cancellationToken)
    {
        var payout = await payoutOrchestrator.TriggerAsync(id, cancellationToken: cancellationToken);
        return Ok(await LoadDtoAsync(payout.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<AdminPayoutDto>> Retry(Guid id, CancellationToken cancellationToken)
    {
        var payout = await payoutOrchestrator.TriggerAsync(id, isRetry: true, cancellationToken: cancellationToken);
        return Ok(await LoadDtoAsync(payout.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await payoutOrchestrator.CancelAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/mark-paid")]
    public async Task<ActionResult<AdminPayoutDto>> MarkPaid(
        Guid id,
        [FromBody] AdminMarkPayoutPaidRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TransferReference))
        {
            return BadRequest(new { error = "TRANSFER_REFERENCE_REQUIRED" });
        }

        var payout = await payoutOrchestrator.MarkPaidAsync(
            id,
            request.TransferReference,
            request.ProviderTransferId,
            cancellationToken);
        return Ok(await LoadDtoAsync(payout.Id, cancellationToken));
    }

    private async Task<AdminPayoutDto> LoadDtoAsync(Guid payoutId, CancellationToken cancellationToken)
    {
        return await context.Payouts
            .AsNoTracking()
            .Include(item => item.Settlement)
            .Where(item => item.Id == payoutId)
            .Select(item => new AdminPayoutDto(
                item.Id,
                item.SettlementId,
                item.Settlement.OwnerType.ToString(),
                item.Settlement.OwnerId,
                item.Amount,
                item.Status.ToString(),
                item.ProviderName,
                item.ProviderTransferId,
                item.TransferReference,
                item.FailureReason,
                item.TriggeredAtUtc,
                item.CompletedAtUtc))
            .FirstAsync(cancellationToken);
    }
}

public sealed record AdminPayoutListDto(
    IReadOnlyList<AdminPayoutDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminPayoutDto(
    Guid Id,
    Guid SettlementId,
    string OwnerType,
    Guid OwnerId,
    decimal Amount,
    string Status,
    string ProviderName,
    string? ProviderTransferId,
    string? TransferReference,
    string? FailureReason,
    DateTime? TriggeredAtUtc,
    DateTime? CompletedAtUtc);

public sealed record AdminPayoutDetailDto(
    AdminPayoutDto Payout,
    IReadOnlyList<AdminPayoutAttemptDto> Attempts);

public sealed record AdminPayoutAttemptDto(
    Guid Id,
    string AttemptType,
    string Status,
    string? ProviderTransferId,
    string? TransferReference,
    string? FailureReason,
    DateTime CreatedAtUtc);

public sealed record AdminMarkPayoutPaidRequest(
    string TransferReference,
    string? ProviderTransferId);
