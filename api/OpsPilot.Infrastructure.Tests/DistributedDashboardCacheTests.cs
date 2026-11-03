using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using OpsPilot.Application.Dashboard;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Caching;
using Xunit;

namespace OpsPilot.Infrastructure.Tests;

/// <summary>Redis palsu di memori. Bisa dipaksa gagal untuk mensimulasikan Redis yang mati.</summary>
internal sealed class FakeDistributedCache : IDistributedCache
{
    public Dictionary<string, byte[]> Store { get; } = new();
    public Dictionary<string, DistributedCacheEntryOptions> Options { get; } = new();

    public bool FailOnGet { get; set; }
    public bool FailOnSet { get; set; }
    public bool FailOnRemove { get; set; }

    public int RemoveCalls { get; private set; }

    public byte[]? Get(string key)
    {
        if (FailOnGet) throw new InvalidOperationException("Simulated Redis outage (get).");
        return Store.TryGetValue(key, out var value) ? value : null;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        if (FailOnSet) throw new InvalidOperationException("Simulated Redis outage (set).");
        Store[key] = value;
        Options[key] = options;
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        RemoveCalls++;
        if (FailOnRemove) throw new InvalidOperationException("Simulated Redis outage (remove).");
        Store.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public void Refresh(string key) { }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
}

public class DistributedDashboardCacheTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private static readonly DateTimeOffset Moment = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeDistributedCache _redis = new();
    private readonly DistributedDashboardCache _sut;

    public DistributedDashboardCacheTests()
    {
        _sut = new DistributedDashboardCache(_redis, NullLogger<DistributedDashboardCache>.Instance);
    }

    private static DashboardPeriodDto Period() => new(7, Moment.AddDays(-6), Moment);

    private static DashboardSummaryDto Summary(int open) => new(Period(), open, 1, 2, 3, 90.5, Moment);

    private async Task<DashboardSummaryDto> GetAsync(string key, Func<DashboardSummaryDto> compute) =>
        await _sut.GetOrCreateAsync(key, Ttl, _ => Task.FromResult(compute()), CancellationToken.None);

    // ==================== Hit dan miss ====================

    [Fact]
    public async Task OnAMiss_RunsTheFactory_AndStoresTheResultWithTheTtl()
    {
        var calls = 0;

        var result = await GetAsync("k1", () => { calls++; return Summary(5); });

        Assert.Equal(5, result.OpenIncidents);
        Assert.Equal(1, calls);
        Assert.True(_redis.Store.ContainsKey("k1"));
        Assert.Equal(Ttl, _redis.Options["k1"].AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task OnAHit_ReturnsTheStoredValue_WithoutRunningTheFactoryAgain()
    {
        var calls = 0;

        var first = await GetAsync("k1", () => { calls++; return Summary(5); });
        var second = await GetAsync("k1", () => { calls++; return Summary(99); });

        Assert.Equal(1, calls);                  // database hanya dibaca sekali
        Assert.Equal(first, second);             // nilai yang sama persis (termasuk waktu dan angka desimal)
        Assert.Equal(5, second.OpenIncidents);
    }

    [Fact]
    public async Task DifferentKeys_AreStoredSeparately()
    {
        await GetAsync("team-a", () => Summary(1));
        await GetAsync("team-b", () => Summary(2));

        Assert.Equal(1, (await GetAsync("team-a", () => Summary(99))).OpenIncidents);
        Assert.Equal(2, (await GetAsync("team-b", () => Summary(99))).OpenIncidents);
    }

    // ==================== Bentuk data bertahan setelah disimpan dan dibaca lagi ====================

    [Fact]
    public async Task BreakdownAndTrends_SurviveTheRoundTripThroughTheCache()
    {
        var breakdown = new DashboardBreakdownDto(
            Period(),
            new[] { new SeverityCountDto(IncidentSeverity.Critical, 2), new SeverityCountDto(IncidentSeverity.Low, 1) },
            new[] { new StatusCountDto(IncidentStatus.Open, 3) },
            Moment);

        var trends = new DashboardTrendsDto(
            Period(),
            new[] { new TrendPointDto(new DateOnly(2026, 10, 30), 4, 1) },
            Moment);

        await _sut.GetOrCreateAsync("b", Ttl, _ => Task.FromResult(breakdown), CancellationToken.None);
        await _sut.GetOrCreateAsync("t", Ttl, _ => Task.FromResult(trends), CancellationToken.None);

        // Kedua kali ini dibaca dari cache (factory yang melempar error membuktikannya)
        var cachedBreakdown = await _sut.GetOrCreateAsync<DashboardBreakdownDto>(
            "b", Ttl, _ => throw new InvalidOperationException("Should be served from cache."), CancellationToken.None);
        var cachedTrends = await _sut.GetOrCreateAsync<DashboardTrendsDto>(
            "t", Ttl, _ => throw new InvalidOperationException("Should be served from cache."), CancellationToken.None);

        Assert.Equal(IncidentSeverity.Critical, cachedBreakdown.BySeverity[0].Severity);
        Assert.Equal(2, cachedBreakdown.BySeverity[0].Count);
        Assert.Equal(IncidentStatus.Open, cachedBreakdown.ByStatus[0].Status);
        Assert.Equal(Moment, cachedBreakdown.GeneratedAtUtc);

        Assert.Equal(new DateOnly(2026, 10, 30), cachedTrends.Points[0].Date);
        Assert.Equal(4, cachedTrends.Points[0].Created);
        Assert.Equal(1, cachedTrends.Points[0].Resolved);
    }

    // ==================== Redis bermasalah: fallback ke database ====================

    [Fact]
    public async Task WhenRedisIsDown_StillReturnsTheValue_FromTheFactory()
    {
        _redis.FailOnGet = true;
        _redis.FailOnSet = true;
        var calls = 0;

        var first = await GetAsync("k1", () => { calls++; return Summary(5); });
        var second = await GetAsync("k1", () => { calls++; return Summary(6); });

        Assert.Equal(5, first.OpenIncidents);       // tidak ada exception ke pemanggil
        Assert.Equal(6, second.OpenIncidents);      // tanpa cache, selalu dihitung ulang
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task WhenOnlyWritingFails_TheFreshValueIsStillReturned()
    {
        _redis.FailOnSet = true;

        var result = await GetAsync("k1", () => Summary(7));

        Assert.Equal(7, result.OpenIncidents);
    }

    [Fact]
    public async Task AStoredValueThatIsCorrupt_IsIgnored_AndReplaced()
    {
        _redis.Store["k1"] = Encoding.UTF8.GetBytes("this is not json at all");
        var calls = 0;

        var first = await GetAsync("k1", () => { calls++; return Summary(5); });
        var second = await GetAsync("k1", () => { calls++; return Summary(99); });

        Assert.Equal(5, first.OpenIncidents);       // rusak -> dihitung ulang dari database
        Assert.Equal(5, second.OpenIncidents);      // salinan rusak sudah diganti yang benar
        Assert.Equal(1, calls);
    }

    // ==================== Penghapusan per tim ====================

    [Fact]
    public async Task InvalidateTeam_RemovesAllTwelveKeysOfThatTeam_AndLeavesOtherTeamsAlone()
    {
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        foreach (var key in DashboardCacheKeys.AllForTeam(teamA).Concat(DashboardCacheKeys.AllForTeam(teamB)))
            await GetAsync(key, () => Summary(1));

        await _sut.InvalidateTeamAsync(teamA, CancellationToken.None);

        Assert.All(DashboardCacheKeys.AllForTeam(teamA), key => Assert.False(_redis.Store.ContainsKey(key)));
        Assert.All(DashboardCacheKeys.AllForTeam(teamB), key => Assert.True(_redis.Store.ContainsKey(key)));
        Assert.Equal(12, _redis.RemoveCalls);
    }

    [Fact]
    public async Task InvalidateTeam_WhenRedisIsDown_DoesNotThrow_AndStopsAfterTheFirstFailure()
    {
        _redis.FailOnRemove = true;

        await _sut.InvalidateTeamAsync(Guid.NewGuid(), CancellationToken.None);   // tidak boleh melempar error

        Assert.Equal(1, _redis.RemoveCalls);      // tidak mencoba 12 kali
    }
}