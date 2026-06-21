using Zadana.Application.Modules.EmailCenter.Interfaces;

namespace Zadana.Api.BackgroundJobs;

public sealed class VendorWeeklySummaryEmailWorker : BackgroundService
{
    private static readonly TimeSpan ScheduledLocalTime = TimeSpan.FromHours(9);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VendorWeeklySummaryEmailWorker> _logger;
    private readonly TimeZoneInfo _saudiTimeZone;

    public VendorWeeklySummaryEmailWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<VendorWeeklySummaryEmailWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _saudiTimeZone = ResolveSaudiTimeZone();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VendorWeeklySummaryEmailWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc = DateTime.UtcNow;
            var nextRunLocal = ResolveNextRunLocal(nowUtc, _saudiTimeZone);
            var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(nextRunLocal, _saudiTimeZone);
            var delay = nextRunUtc - nowUtc;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.FromMinutes(1);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
                await DispatchForScheduledWeekAsync(nextRunLocal, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VendorWeeklySummaryEmailWorker encountered an error.");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        _logger.LogInformation("VendorWeeklySummaryEmailWorker stopped.");
    }

    private async Task DispatchForScheduledWeekAsync(
        DateTime scheduledRunLocal,
        CancellationToken cancellationToken)
    {
        var currentWeekStartLocal = scheduledRunLocal.Date;
        var previousWeekStartLocal = currentWeekStartLocal.AddDays(-7);
        var weekStartUtc = TimeZoneInfo.ConvertTimeToUtc(previousWeekStartLocal, _saudiTimeZone);
        var weekEndUtc = TimeZoneInfo.ConvertTimeToUtc(currentWeekStartLocal, _saudiTimeZone);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var summaryService = scope.ServiceProvider.GetRequiredService<IVendorWeeklySummaryEmailService>();
        var sent = await summaryService.DispatchWeeklySummariesAsync(weekStartUtc, weekEndUtc, cancellationToken);

        _logger.LogInformation(
            "Vendor weekly summaries completed for {WeekStartUtc} to {WeekEndUtc}. Sent: {Sent}.",
            weekStartUtc,
            weekEndUtc,
            sent);
    }

    private static DateTime ResolveNextRunLocal(DateTime nowUtc, TimeZoneInfo timeZone)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)localNow.DayOfWeek + 7) % 7;
        var candidate = localNow.Date.AddDays(daysUntilMonday).Add(ScheduledLocalTime);

        if (candidate <= localNow)
        {
            candidate = candidate.AddDays(7);
        }

        return DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified);
    }

    private static TimeZoneInfo ResolveSaudiTimeZone()
    {
        foreach (var id in new[] { "Asia/Riyadh", "Arab Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Saudi Arabia Standard Time",
            TimeSpan.FromHours(3),
            "Saudi Arabia Standard Time",
            "Saudi Arabia Standard Time");
    }
}
