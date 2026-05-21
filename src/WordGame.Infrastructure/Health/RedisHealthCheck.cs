using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace WordGame.Infrastructure.Health;

public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis OK: {latency.TotalMilliseconds:n0} ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis indisponibil.", ex);
        }
    }
}
