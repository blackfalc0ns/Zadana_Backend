using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Api.BackgroundJobs;

/// <summary>
/// Monitors support case SLA compliance, auto-escalates breached cases,
/// sends stale-evidence reminders, and auto-closes abandoned cases.
/// </summary>
public class SupportCaseSlaWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 50;
    private const int AutoEscalateAfterHours = 2;
    private const int StaleEvidenceReminderHours = 72;
    private const int AutoCloseStaleDays = 14;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SupportCaseSlaWorker> _logger;

    public SupportCaseSlaWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SupportCaseSlaWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SupportCaseSlaWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SupportCaseSlaWorker encountered an error.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }

        _logger.LogInformation("SupportCaseSlaWorker stopped.");
    }

    private async Task ProcessTickAsync(CancellationToken cancellationToken)
    {
        await DetectSlaBreachesAsync(cancellationToken);
        await AutoEscalateSevereBreachesAsync(cancellationToken);
        await SendStaleEvidenceRemindersAsync(cancellationToken);
        await AutoCloseAbandonedCasesAsync(cancellationToken);
    }

    /// <summary>
    /// Check 1: Flag cases whose SLA has been breached but not yet recorded.
    /// </summary>
    private async Task DetectSlaBreachesAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var caseIds = await QueryCaseIdsAsync(
            query => query.Where(c =>
                c.Status != OrderSupportCaseStatus.Rejected &&
                c.Status != OrderSupportCaseStatus.Resolved &&
                c.SlaDueAtUtc != null &&
                c.SlaDueAtUtc < now &&
                !c.Activities.Any(a => a.Action == "sla_breached"))
                .OrderBy(c => c.SlaDueAtUtc),
            cancellationToken);

        var processed = 0;
        foreach (var caseId in caseIds)
        {
            processed += await TryProcessCaseAsync(
                caseId,
                supportCase =>
                {
                    if (supportCase.Activities.Any(a => a.Action == "sla_breached"))
                    {
                        return false;
                    }

                    supportCase.RecordSlaBreach();
                    return true;
                },
                "sla_breached",
                cancellationToken);
        }

        if (processed > 0)
        {
            _logger.LogWarning(
                "SLA breach detected for {Count} support cases.",
                processed);
        }
    }

    /// <summary>
    /// Check 2: Auto-escalate cases whose SLA was breached 2+ hours ago.
    /// </summary>
    private async Task AutoEscalateSevereBreachesAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-AutoEscalateAfterHours);

        var caseIds = await QueryCaseIdsAsync(
            query => query.Where(c =>
                c.Status != OrderSupportCaseStatus.Rejected &&
                c.Status != OrderSupportCaseStatus.Resolved &&
                c.SlaDueAtUtc != null &&
                c.SlaDueAtUtc < cutoff &&
                c.Priority != OrderSupportCasePriority.Critical &&
                !c.Activities.Any(a => a.Action == "auto_escalated"))
                .OrderBy(c => c.SlaDueAtUtc),
            cancellationToken);

        var processed = 0;
        foreach (var caseId in caseIds)
        {
            processed += await TryProcessCaseAsync(
                caseId,
                supportCase =>
                {
                    if (supportCase.Activities.Any(a => a.Action == "auto_escalated"))
                    {
                        return false;
                    }

                    var newPriority = supportCase.Priority switch
                    {
                        OrderSupportCasePriority.Low => OrderSupportCasePriority.Medium,
                        OrderSupportCasePriority.Medium => OrderSupportCasePriority.High,
                        OrderSupportCasePriority.High => OrderSupportCasePriority.Critical,
                        _ => OrderSupportCasePriority.High
                    };

                    supportCase.AutoEscalate(
                        supportCase.Queue,
                        newPriority,
                        $"Auto-escalated: SLA breached by {AutoEscalateAfterHours}+ hours. Priority changed to {newPriority}.",
                        slaDueAtUtc: DateTime.UtcNow.AddHours(4));

                    return true;
                },
                "auto_escalated",
                cancellationToken);
        }

        if (processed > 0)
        {
            _logger.LogWarning(
                "Auto-escalated {Count} support cases due to severe SLA breach.",
                processed);
        }
    }

    /// <summary>
    /// Check 3: Remind about cases awaiting evidence for 72+ hours with no response.
    /// </summary>
    private async Task SendStaleEvidenceRemindersAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-StaleEvidenceReminderHours);

        var caseIds = await QueryCaseIdsAsync(
            query => query.Where(c =>
                c.Status != OrderSupportCaseStatus.Rejected &&
                c.Status != OrderSupportCaseStatus.Resolved &&
                c.AwaitingResponseFromRole != null &&
                c.UpdatedAtUtc < cutoff &&
                !c.Activities.Any(a =>
                    a.Action == "stale_evidence_reminder" &&
                    a.CreatedAtUtc > cutoff))
                .OrderBy(c => c.UpdatedAtUtc),
            cancellationToken);

        var processed = 0;
        foreach (var caseId in caseIds)
        {
            processed += await TryProcessCaseAsync(
                caseId,
                supportCase =>
                {
                    var reminderCutoff = DateTime.UtcNow.AddHours(-StaleEvidenceReminderHours);
                    if (supportCase.Activities.Any(a =>
                            a.Action == "stale_evidence_reminder" &&
                            a.CreatedAtUtc > reminderCutoff))
                    {
                        return false;
                    }

                    supportCase.RecordStaleEvidenceReminder();
                    return true;
                },
                "stale_evidence_reminder",
                cancellationToken);
        }

        if (processed > 0)
        {
            _logger.LogInformation(
                "Sent stale evidence reminders for {Count} support cases.",
                processed);
        }
    }

    /// <summary>
    /// Check 4: Auto-close cases abandoned for 14+ days with no response.
    /// </summary>
    private async Task AutoCloseAbandonedCasesAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-AutoCloseStaleDays);

        var caseIds = await QueryCaseIdsAsync(
            query => query.Where(c =>
                c.Status != OrderSupportCaseStatus.Rejected &&
                c.Status != OrderSupportCaseStatus.Resolved &&
                c.AwaitingResponseFromRole != null &&
                c.UpdatedAtUtc < cutoff)
                .OrderBy(c => c.UpdatedAtUtc),
            cancellationToken);

        var processed = 0;
        foreach (var caseId in caseIds)
        {
            processed += await TryProcessCaseAsync(
                caseId,
                supportCase =>
                {
                    supportCase.Resolve(
                        Guid.Empty,
                        $"Auto-closed: no response from {supportCase.AwaitingResponseFromRole} for {AutoCloseStaleDays}+ days.");

                    return true;
                },
                "auto_close_abandoned",
                cancellationToken);
        }

        if (processed > 0)
        {
            _logger.LogInformation(
                "Auto-closed {Count} abandoned support cases after {Days} days of inactivity.",
                processed,
                AutoCloseStaleDays);
        }
    }

    private async Task<List<Guid>> QueryCaseIdsAsync(
        Func<IQueryable<OrderSupportCase>, IOrderedQueryable<OrderSupportCase>> filter,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        return await filter(context.OrderSupportCases.AsNoTracking())
            .Select(c => c.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task<int> TryProcessCaseAsync(
        Guid caseId,
        Func<OrderSupportCase, bool> apply,
        string actionName,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var supportCase = await context.OrderSupportCases
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);

        if (supportCase is null)
        {
            return 0;
        }

        try
        {
            if (!apply(supportCase))
            {
                return 0;
            }

            await context.SaveChangesAsync(cancellationToken);
            return 1;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                ex,
                "SupportCaseSlaWorker skipped support case {CaseId} for action {Action} due to a concurrent update.",
                caseId,
                actionName);

            return 0;
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogDebug(
                ex,
                "SupportCaseSlaWorker skipped support case {CaseId} for action {Action}: {Message}",
                caseId,
                actionName,
                ex.Message);

            return 0;
        }
    }
}
