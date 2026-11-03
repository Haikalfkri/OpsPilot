using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Dashboard;

namespace OpsPilot.Infrastructure.Caching;

public sealed class DistributedDashboardCache(
    IDistributedCache cache,
    ILogger<DistributedDashboardCache> logger) : IDashboardCache
{
    public async Task<T> GetOrCreateAsync<T>(
        string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
        where T : class
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);

            if (bytes is not null)
            {
                var cached = JsonSerializer.Deserialize<T>(bytes);

                if (cached is not null)
                {
                    logger.LogDebug("Dashboard cache HIT {Key}", key);
                    return cached;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Dashboard cache read failed for {Key}. Falling back to the database.", key);
        }

        logger.LogDebug("Dashboard cache MISS {Key}", key);
        var fresh = await factory(ct);

        try
        {
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(fresh), options, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Dashboard cache write failed for {Key}.", key);
        }

        return fresh;
    }

    public async Task InvalidateTeamAsync(Guid teamId, CancellationToken ct)
    {
        foreach (var key in DashboardCacheKeys.AllForTeam(teamId))
        {
            try
            {
                await cache.RemoveAsync(key, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Dashboard cache invalidation failed for team {TeamId}.", teamId);
                return;
            }
        }
    }
}