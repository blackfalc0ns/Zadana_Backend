using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Settings;

namespace Zadana.Infrastructure.Caching;

public sealed class RedisDistributedCacheHealthCheck(
    IDistributedCache distributedCache,
    IHostEnvironment environment,
    IOptions<CachingSettings> cachingOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var settings = cachingOptions.Value;
        if (!environment.IsProduction() || !settings.Redis.RequireInProduction)
        {
            return HealthCheckResult.Healthy("Redis caching is optional outside production.");
        }

        if (string.IsNullOrWhiteSpace(settings.Redis.ConnectionString))
        {
            return HealthCheckResult.Unhealthy("Redis connection string is required in production.");
        }

        try
        {
            await distributedCache.GetAsync("health:cache:probe", cancellationToken);
            return HealthCheckResult.Healthy("Redis distributed cache is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis distributed cache is unavailable.", exception);
        }
    }
}
