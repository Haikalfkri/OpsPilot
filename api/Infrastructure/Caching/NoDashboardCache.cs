using OpsPilot.Application.Common.Interfaces;

namespace OpsPilot.Infrastructure.Caching
{
    public sealed class NoDashboardCache : IDashboardCache
    {
        public Task<T> GetOrCreateAsync<T>(
            string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
            where T : class => factory(ct);

        public Task InvalidateTeamAsync(Guid teamId, CancellationToken ct) => Task.CompletedTask;
    }
}
