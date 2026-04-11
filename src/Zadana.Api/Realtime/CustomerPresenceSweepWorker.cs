using Microsoft.Extensions.Hosting;

namespace Zadana.Api.Realtime;

public sealed class CustomerPresenceSweepWorker : BackgroundService
{
    private readonly CustomerPresenceService _customerPresenceService;

    public CustomerPresenceSweepWorker(CustomerPresenceService customerPresenceService)
    {
        _customerPresenceService = customerPresenceService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _customerPresenceService.SweepAsync(stoppingToken);
        }
    }
}
