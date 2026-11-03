using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Dashboard;

namespace OpsPilot.Application.Tests.Dashboard;

/// <summary>Tanpa cache: selalu menghitung ulang. Dipakai tes yang hanya menguji angkanya.</summary>
public sealed class PassThroughDashboardCache : IDashboardCache
{
    public Task<T> GetOrCreateAsync<T>(
        string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
        where T : class => factory(ct);

    public Task InvalidateTeamAsync(Guid teamId, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// Meniru cache sungguhan di memori: menyimpan hasil per kunci sampai dihapus.
/// Menghitung berapa kali database benar-benar dibaca (FactoryCalls).
/// </summary>
public sealed class InMemoryDashboardCache : IDashboardCache
{
    private readonly Dictionary<string, object> _store = new();

    public int FactoryCalls { get; private set; }
    public TimeSpan? LastTtl { get; private set; }

    public bool Contains(string key) => _store.ContainsKey(key);

    public async Task<T> GetOrCreateAsync<T>(
        string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
        where T : class
    {
        if (_store.TryGetValue(key, out var cached))
            return (T)cached;

        FactoryCalls++;
        LastTtl = ttl;

        var value = await factory(ct);
        _store[key] = value;
        return value;
    }

    public Task InvalidateTeamAsync(Guid teamId, CancellationToken ct)
    {
        foreach (var key in DashboardCacheKeys.AllForTeam(teamId))
            _store.Remove(key);

        return Task.CompletedTask;
    }
}