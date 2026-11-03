namespace OpsPilot.Application.Common.Interfaces
{
    public interface IDashboardCache
    {
        Task<T> GetOrCreateAsync<T>(
            string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
            where T : class;

        Task InvalidateTeamAsync(Guid teamId, CancellationToken ct);
    }
}
