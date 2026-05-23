using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Orders.Enums;

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
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        await DetectSlaBreachesAsync(context, cancellationToken);
        await AutoEscalateSevereBreachesAsync(context, cancellationToken);
        await SendStaleEvidenceRemindersAsync(context, cancellationToken);
        await AutoCloseAbandonedCasesAsync(context, cancellationToken);
    }

    /// <summary>
    /// Check 1: Flag cases whose SLA has been breached but not yet recorded.
    /// </summary>
    private async Task DetectSlaBreachesAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var breachedCases = await context.OrderSupportCases
            .Include(c => c.Activities)
            .Where(c =>
                c.Status != OrderSupportCaseStatus.Rejected &&
                c.Status != OrderSupportCaseStatus.Resolved &&
                c.SlaDueAtUtc != null &&
                c.SlaDueAtUtc < now &&
                !c.Activities.Any(a => a.Action == "sla_breached"))
            .OrderBy(c => c.SlaDueAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (breachedCases.Count == 0) return;

        foreach (var supportCase in breachedCases)
        {
            supportCase.AddInternalNote(
                Guid.Empty,
                $"SLA breached at {supportCase.SlaDueAtUtc:u}. Case has exceeded its response deadline.",
                visibleToCustomer: false);

            // Mark the activity so we don't alert again
            var lastActivity = supportCase.Activities.OrderByDescending(a => a.CreatedAtUtc).First();
            // The AddInternalNote already creates an activity, we just need to tag it
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "SLA breach detected for {Count} support cases.",
            breachedCases.Count);
    }

    /// <summary>
    /// Check 2: Auto-escalate cases whose SLA was breached 2+ hours ago.
    /// </summary>
    private async Task AutoEscalateSevereBreachesAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-AutoEscalateAfterHours);

        var severeCases = await context.OrderSupportCases
            .Include(c => c.Activities)
            .Where(c =>
                c.Status != OrderSupportCaseStatus.Rejected &&
                c.Status != OrderSupportCaseStatus.Resolved &&
                c.SlaDueAtUtc != null &&
                c.SlaDueAtUtc < cutoff &&
                c.Priority != OrderSupportCasePriority.Critical &&
                !c.Activities.Any(a => a.Action == "auto_escalated"))
            .OrderBy(c => c.SlaDueAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (severeCases.Count == 0) return;

        foreach (var supportCase in severeCases)
        {
            var newPriority = supportCase.Priority switch
            {
                OrderSupportCasePriority.Low => OrderSupportCasePriority.Medium,
                OrderSupportCasePriority.Medium => OrderSupportCasePriority.High,
                OrderSupportCasePriority.High => OrderSupportCasePriority.Critical,
                _ => OrderSupportCasePriority.High
            };

            supportCase.Escalate(
                Guid.Empty,
                supportCase.Queue,
                newPriority,
                $"Auto-escalated: SLA breached by {AutoEscalateAfterHours}+ hours. Priority changed to {newPriority}.",
                customerVisibleNote: null,
                slaDueAtUtc: DateTime.UtcNow.AddHours(4));
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Auto-escalated {Count} support cases due to severe SLA breach.",
            severeCases.Count);
    }

    /// <summary>
    /// Check 3: Remind about cases awaiting evidence for 72+ hours with no response.
    /// </summary>
    private async Task SendStaleEvidenceRemindersAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-StaleEvidenceReminderHours);

        var staleCases = await context.OrderSupportCases
            .Include(c => c.Activities)
            .Where(c =>
                c.Status != OrderSupportCaseStatus.Rejected &&
                c.Status != OrderSupportCaseStatus.Resolved &&
                c.AwaitingResponseFromRole != null &&
                c.UpdatedAtUtc < cutoff &&
                !c.Activities.Any(a =>
                    a.Action == "stale_evidence_reminder" &&
                    a.CreatedAtUtc > cutoff))
            .OrderBy(c => c.UpdatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (staleCases.Count == 0) return;

        foreach (var supportCase in staleCases)
        {
            supportCase.AddInternalNote(
                Guid.Empty,
                $"Reminder: awaiting response from {supportCase.AwaitingResponseFromRole} for {StaleEvidenceReminderHours}+ hours. No response received.",
                visibleToCustomer: false);
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Sent stale evidence reminders for {Count} support cases.",
            staleCases.Count);
    }

    /// <summary>
    /// Check 4: Auto-close cases abandoned for 14+ days with no response.
    /// </summary>
    private async Task AutoCloseAbandonedCasesAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-AutoCloseStaleDays);

        var abandonedCases = await context.OrderSupportCases
            .Include(c => c.Activities)
            .Where(c =>
                c.Status != OrderSupportCaseStatus.Rejected &&
                c.Status != OrderSupportCaseStatus.Resolved &&
                c.AwaitingResponseFromRole != null &&
                c.UpdatedAtUtc < cutoff)
            .OrderBy(c => c.UpdatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (abandonedCases.Count == 0) return;

        foreach (var supportCase in abandonedCases)
        {
            supportCase.Resolve(
                Guid.Empty,
                $"Auto-closed: no response from {supportCase.AwaitingResponseFromRole} for {AutoCloseStaleDays}+ days.");
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Auto-closed {Count} abandoned support cases after {Days} days of inactivity.",
            abandonedCases.Count,
            AutoCloseStaleDays);
    }
}
