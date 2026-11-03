using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Dashboard;
using OpsPilot.Application.Tests.Incidents;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Application.Tests.Dashboard;

public class DashboardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private readonly TestDbContext _db = TestDbContext.CreateNew();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly MutableTimeProvider _clock = new(Now);
    private readonly Guid _teamId = Guid.NewGuid();
    private readonly DashboardService _sut;   // tanpa cache: selalu menghitung ulang

    public DashboardServiceTests()
    {
        _currentUser.TeamId.Returns(_teamId);
        _currentUser.UserId.Returns(Guid.NewGuid());
        _currentUser.Role.Returns(UserRole.Viewer);    // semua role boleh melihat dashboard

        _sut = CreateService(new PassThroughDashboardCache());
    }

    // ---------- Helper ----------

    private DashboardService CreateService(IDashboardCache cache) =>
        new(new DashboardQueryValidator(), _db, cache, _currentUser, _clock);

    /// <summary>Tanggal UTC yang mudah dibaca: At(10, 29, 8) = 29 Oktober 08:00.</summary>
    private static DateTimeOffset At(int month, int day, int hour = 0, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    private Incident Seed(
        IncidentSeverity severity = IncidentSeverity.Medium,
        DateTimeOffset? createdAt = null,
        Guid? teamId = null,
        Action<Incident>? configure = null)
    {
        var incident = Incident.Create(
            teamId ?? _teamId, Guid.NewGuid(), "Seeded incident", "Seeded description",
            severity, null, SlaPolicy.Default(), createdAt ?? Now);

        configure?.Invoke(incident);   // mis. memindahkan status sebelum disimpan

        _db.Incidents.Add(incident);
        _db.SaveChanges();
        return incident;
    }

    private static Action<Incident> Investigating(DateTimeOffset at) => i => i.ChangeStatus(IncidentStatus.Investigating, at);

    private static Action<Incident> Resolved(DateTimeOffset at) => i => i.ChangeStatus(IncidentStatus.Resolved, at);

    private static Action<Incident> Closed(DateTimeOffset resolvedAt, DateTimeOffset closedAt) => i =>
    {
        i.ChangeStatus(IncidentStatus.Resolved, resolvedAt);
        i.ChangeStatus(IncidentStatus.Closed, closedAt);
    };

    // ==================== Summary: tim kosong ====================

    [Fact]
    public async Task Summary_ForAnEmptyTeam_ReturnsZeros_AndNoAverage()
    {
        var result = await _sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(0, result.OpenIncidents);
        Assert.Equal(0, result.CriticalHighOpenIncidents);
        Assert.Equal(0, result.SlaBreachedIncidents);
        Assert.Equal(0, result.ResolvedInPeriod);
        Assert.Null(result.AverageResolutionMinutes);
        Assert.Equal(7, result.Period.Days);
        Assert.Equal(At(10, 24), result.Period.FromUtc);
        Assert.Equal(Now, result.Period.ToUtc);
        Assert.Equal(Now, result.GeneratedAtUtc);
    }

    // ==================== Summary: open ====================

    [Fact]
    public async Task Summary_OpenIncidents_CountsOnlyOpenAndInvestigating()
    {
        Seed();                                              // Open
        Seed(configure: Investigating(Now));                 // Investigating
        Seed(configure: Resolved(Now));                      // Resolved: tidak dihitung
        Seed(configure: Closed(Now, Now));                   // Closed: tidak dihitung

        var result = await _sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(2, result.OpenIncidents);
    }

    [Fact]
    public async Task Summary_CriticalHighOpen_CountsOnlyActiveCriticalAndHigh()
    {
        Seed(IncidentSeverity.Critical);                                       // dihitung
        Seed(IncidentSeverity.High, configure: Investigating(Now));            // dihitung
        Seed(IncidentSeverity.Medium);                                         // bukan Critical/High
        Seed(IncidentSeverity.Low);
        Seed(IncidentSeverity.Critical, configure: Resolved(Now));             // sudah selesai

        var result = await _sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(4, result.OpenIncidents);
        Assert.Equal(2, result.CriticalHighOpenIncidents);
    }

    [Fact]
    public async Task Summary_NeverCountsAnotherTeamsIncidents()
    {
        var otherTeam = Guid.NewGuid();
        Seed(IncidentSeverity.Critical, teamId: otherTeam);
        Seed(IncidentSeverity.Critical, teamId: otherTeam);
        Seed(IncidentSeverity.High, teamId: otherTeam, configure: Resolved(Now));
        Seed(IncidentSeverity.Low);                                            // satu-satunya milik tim saya

        var result = await _sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(1, result.OpenIncidents);
        Assert.Equal(0, result.CriticalHighOpenIncidents);
        Assert.Equal(0, result.ResolvedInPeriod);
    }

    // ==================== Summary: melanggar SLA ====================

    [Fact]
    public async Task Summary_SlaBreached_CountsActiveIncidentsPastEitherTarget_AndAgreesWithTheDomainRules()
    {
        // Critical: respons 15 menit, penyelesaian 4 jam (dari waktu dibuat). Sekarang = 08:00.
        Seed(IncidentSeverity.Critical, createdAt: Now.AddHours(-1));                          // A: belum direspons, batas 15 menit lewat -> terlambat
        Seed(IncidentSeverity.Critical, createdAt: Now.AddHours(-1),
             configure: Investigating(Now.AddMinutes(-55)));                                   // B: direspons tepat waktu, penyelesaian belum lewat -> aman
        Seed(IncidentSeverity.Low, createdAt: Now.AddHours(-1));                               // C: batas Low masih jauh -> aman
        Seed(IncidentSeverity.Critical, createdAt: Now.AddHours(-5),
             configure: Investigating(Now.AddHours(-5).AddMinutes(10)));                       // D: direspons tepat waktu, tapi batas 4 jam lewat -> terlambat
        Seed(IncidentSeverity.Critical, createdAt: Now.AddHours(-5),
             configure: Resolved(Now.AddHours(-3)));                                           // E: sudah Resolved -> tidak dihitung
        Seed(IncidentSeverity.Critical, createdAt: Now.AddHours(-1),
             configure: Investigating(Now.AddMinutes(-30)));                                   // F: direspons terlambat (30 menit > 15) -> terlambat

        var result = await _sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(3, result.SlaBreachedIncidents);

        // Bukti konsistensi: angka dashboard sama dengan penilaian aturan Domain (SlaEvaluator) per incident
        var all = await _db.Incidents.AsNoTracking().ToListAsync();
        var expected = all.Count(i =>
            (i.Status == IncidentStatus.Open || i.Status == IncidentStatus.Investigating)
            && (i.GetResponseSla(Now) == SlaState.Breached || i.GetResolutionSla(Now) == SlaState.Breached));

        Assert.Equal(expected, result.SlaBreachedIncidents);
    }

    [Fact]
    public async Task Summary_SlaBreached_ExactlyAtTheDueTime_IsStillSafe()
    {
        // Dibuat 15 menit lalu: batas respons Critical (15 menit) jatuh TEPAT sekarang
        Seed(IncidentSeverity.Critical, createdAt: Now.AddMinutes(-15));

        var result = await _sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(0, result.SlaBreachedIncidents);
    }

    // ==================== Summary: resolved dan rata-rata ====================

    [Fact]
    public async Task Summary_Resolved_CountsOnlyThoseResolvedInsideThePeriod_AndAveragesTheirDuration()
    {
        Seed(createdAt: At(10, 29, 8), configure: Resolved(At(10, 29, 9)));             // 60 menit, di dalam periode
        Seed(createdAt: At(10, 29, 10), configure: Resolved(At(10, 29, 12)));           // 120 menit, di dalam periode
        Seed(createdAt: At(10, 20, 8), configure: Resolved(At(10, 23, 23, 59)));        // diselesaikan 1 menit SEBELUM periode

        var result = await _sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(2, result.ResolvedInPeriod);
        Assert.Equal(90.0, result.AverageResolutionMinutes);
    }

    [Fact]
    public async Task Summary_Resolved_TheStartOfThePeriodIsInclusive()
    {
        // Diselesaikan tepat 24 Okt 00:00 = awal periode 7 hari
        Seed(createdAt: At(10, 23, 22), configure: Resolved(At(10, 24, 0)));

        var result = await _sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(1, result.ResolvedInPeriod);
        Assert.Equal(120.0, result.AverageResolutionMinutes);
    }

    [Fact]
    public async Task Summary_Resolved_AlsoCountsIncidentsThatAreNowClosed()
    {
        Seed(createdAt: At(10, 29, 8), configure: Closed(At(10, 29, 9), At(10, 29, 10)));

        var result = await _sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(1, result.ResolvedInPeriod);
        Assert.Equal(60.0, result.AverageResolutionMinutes);     // dihitung sampai Resolved, bukan Closed
    }

    [Fact]
    public async Task Summary_Average_IsRoundedToOneDecimal()
    {
        Seed(createdAt: At(10, 29, 8), configure: Resolved(At(10, 29, 8, 10)));    // 10 menit
        Seed(createdAt: At(10, 29, 9), configure: Resolved(At(10, 29, 9, 15)));    // 15 menit

        var result = await _sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(12.5, result.AverageResolutionMinutes);
    }

    [Fact]
    public async Task Summary_ALongerPeriod_IncludesOlderResolvedIncidents()
    {
        Seed(createdAt: At(10, 18, 8), configure: Resolved(At(10, 18, 9)));     // 12 hari lalu

        var week = await _sut.GetSummaryAsync(new DashboardQuery(7), CancellationToken.None);
        var fortnight = await _sut.GetSummaryAsync(new DashboardQuery(14), CancellationToken.None);

        Assert.Equal(0, week.ResolvedInPeriod);
        Assert.Equal(1, fortnight.ResolvedInPeriod);
    }

    [Fact]
    public async Task Summary_OpenCount_DoesNotDependOnThePeriod()
    {
        Seed(createdAt: Now.AddDays(-60));       // dibuat 60 hari lalu, masih Open

        var result = await _sut.GetSummaryAsync(new DashboardQuery(7), CancellationToken.None);

        Assert.Equal(1, result.OpenIncidents);   // kondisi saat ini, bukan "dibuat dalam periode"
    }

    // ==================== Breakdown ====================

    [Fact]
    public async Task Breakdown_ForAnEmptyTeam_ListsEverySeverityAndStatus_WithZeros()
    {
        var result = await _sut.GetBreakdownAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { IncidentSeverity.Critical, IncidentSeverity.High, IncidentSeverity.Medium, IncidentSeverity.Low },
            result.BySeverity.Select(s => s.Severity).ToArray());
        Assert.Equal(
            new[] { IncidentStatus.Open, IncidentStatus.Investigating, IncidentStatus.Resolved, IncidentStatus.Closed },
            result.ByStatus.Select(s => s.Status).ToArray());
        Assert.All(result.BySeverity, s => Assert.Equal(0, s.Count));
        Assert.All(result.ByStatus, s => Assert.Equal(0, s.Count));
    }

    [Fact]
    public async Task Breakdown_CountsIncidentsCreatedInThePeriod_PerSeverityAndPerStatus()
    {
        Seed(IncidentSeverity.Critical, createdAt: At(10, 29));                                              // Open
        Seed(IncidentSeverity.Critical, createdAt: At(10, 29), configure: Investigating(At(10, 29, 1)));     // Investigating
        Seed(IncidentSeverity.High, createdAt: At(10, 29), configure: Resolved(At(10, 29, 2)));              // Resolved
        Seed(IncidentSeverity.Low, createdAt: At(10, 29), configure: Closed(At(10, 29, 2), At(10, 29, 3)));  // Closed
        Seed(IncidentSeverity.Critical, createdAt: At(10, 20));                                              // sebelum periode: tidak dihitung
        Seed(IncidentSeverity.Critical, createdAt: At(10, 29), teamId: Guid.NewGuid());                      // tim lain: tidak dihitung

        var result = await _sut.GetBreakdownAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(new[] { 2, 1, 0, 1 }, result.BySeverity.Select(s => s.Count).ToArray());   // Critical, High, Medium, Low
        Assert.Equal(new[] { 1, 1, 1, 1 }, result.ByStatus.Select(s => s.Count).ToArray());     // Open, Investigating, Resolved, Closed
    }

    [Fact]
    public async Task Breakdown_ALongerPeriod_IncludesOlderIncidents()
    {
        Seed(IncidentSeverity.Critical, createdAt: At(10, 20));    // 10 hari lalu

        var week = await _sut.GetBreakdownAsync(new DashboardQuery(7), CancellationToken.None);
        var fortnight = await _sut.GetBreakdownAsync(new DashboardQuery(14), CancellationToken.None);

        Assert.Equal(0, week.BySeverity[0].Count);
        Assert.Equal(1, fortnight.BySeverity[0].Count);
    }

    // ==================== Trends ====================

    [Fact]
    public async Task Trends_ForAnEmptyTeam_StillReturnsOnePointPerDay_AllZero()
    {
        var result = await _sut.GetTrendsAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(7, result.Points.Count);
        Assert.Equal(new DateOnly(2026, 10, 24), result.Points.First().Date);
        Assert.Equal(new DateOnly(2026, 10, 30), result.Points.Last().Date);
        Assert.All(result.Points, p =>
        {
            Assert.Equal(0, p.Created);
            Assert.Equal(0, p.Resolved);
        });
    }

    [Fact]
    public async Task Trends_PlacesIncidentsOnTheirUtcDays()
    {
        Seed(createdAt: At(10, 29, 23, 59));                                              // 29 Okt
        Seed(createdAt: At(10, 30, 0, 0));                                                // 30 Okt (tengah malam tepat)
        Seed(createdAt: At(10, 28, 12), configure: Resolved(At(10, 30, 7)));              // dibuat 28 Okt, diselesaikan 30 Okt
        Seed(createdAt: At(10, 20), teamId: _teamId);                                     // sebelum periode: diabaikan
        Seed(createdAt: At(10, 29), teamId: Guid.NewGuid());                              // tim lain: diabaikan

        var result = await _sut.GetTrendsAsync(new DashboardQuery(), CancellationToken.None);

        PointOf(result, new DateOnly(2026, 10, 28), created: 1, resolved: 0);
        PointOf(result, new DateOnly(2026, 10, 29), created: 1, resolved: 0);
        PointOf(result, new DateOnly(2026, 10, 30), created: 1, resolved: 1);
    }

    [Fact]
    public async Task Trends_ResolvedTotal_EqualsTheSummaryResolvedInPeriod()
    {
        Seed(createdAt: At(10, 26, 8), configure: Resolved(At(10, 27, 9)));
        Seed(createdAt: At(10, 29, 8), configure: Resolved(At(10, 29, 9)));
        Seed(createdAt: At(10, 29, 8), configure: Closed(At(10, 29, 9), At(10, 29, 10)));

        var summary = await _sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);
        var trends = await _sut.GetTrendsAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(summary.ResolvedInPeriod, trends.Points.Sum(p => p.Resolved));
    }

    private static void PointOf(DashboardTrendsDto trends, DateOnly date, int created, int resolved)
    {
        var point = trends.Points.Single(p => p.Date == date);
        Assert.Equal(created, point.Created);
        Assert.Equal(resolved, point.Resolved);
    }

    // ==================== Validasi ====================

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(365)]
    public async Task EveryEndpoint_WithAnInvalidPeriod_ThrowsValidation_AndNeverReachesTheCache(int days)
    {
        var cache = new InMemoryDashboardCache();
        var sut = CreateService(cache);

        await Assert.ThrowsAsync<ValidationException>(() => sut.GetSummaryAsync(new DashboardQuery(days), CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => sut.GetBreakdownAsync(new DashboardQuery(days), CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => sut.GetTrendsAsync(new DashboardQuery(days), CancellationToken.None));

        Assert.Equal(0, cache.FactoryCalls);
    }

    // ==================== Cache ====================

    [Fact]
    public async Task Summary_IsServedFromTheCache_UntilItIsInvalidated()
    {
        var cache = new InMemoryDashboardCache();
        var sut = CreateService(cache);
        Seed();

        var first = await sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);
        Assert.Equal(1, first.OpenIncidents);
        Assert.Equal(1, cache.FactoryCalls);

        // Data baru masuk, dan waktu berjalan 30 detik
        Seed();
        _clock.Now = Now.AddSeconds(30);

        var second = await sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(1, second.OpenIncidents);                              // masih salinan lama
        Assert.Equal(first.GeneratedAtUtc, second.GeneratedAtUtc);          // bukti: tidak dihitung ulang
        Assert.Equal(1, cache.FactoryCalls);                                // database tidak dibaca lagi

        // Salinan dibuang (seperti yang dilakukan IncidentService setelah menyimpan)
        await cache.InvalidateTeamAsync(_teamId, CancellationToken.None);

        var third = await sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(2, third.OpenIncidents);                               // angka baru
        Assert.Equal(Now.AddSeconds(30), third.GeneratedAtUtc);
        Assert.Equal(2, cache.FactoryCalls);
    }

    [Fact]
    public async Task Cache_UsesAnExpiryOfSixtySeconds()
    {
        var cache = new InMemoryDashboardCache();

        await CreateService(cache).GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(60), cache.LastTtl);
    }

    [Fact]
    public async Task Cache_KeepsAnEntryPerPeriod_AndPerEndpoint()
    {
        var cache = new InMemoryDashboardCache();
        var sut = CreateService(cache);

        await sut.GetSummaryAsync(new DashboardQuery(7), CancellationToken.None);
        await sut.GetSummaryAsync(new DashboardQuery(14), CancellationToken.None);
        await sut.GetBreakdownAsync(new DashboardQuery(7), CancellationToken.None);
        await sut.GetTrendsAsync(new DashboardQuery(7), CancellationToken.None);
        Assert.Equal(4, cache.FactoryCalls);                 // empat kombinasi berbeda

        // Mengulang yang sama tidak membaca database lagi
        await sut.GetSummaryAsync(new DashboardQuery(7), CancellationToken.None);
        await sut.GetSummaryAsync(new DashboardQuery(14), CancellationToken.None);
        await sut.GetBreakdownAsync(new DashboardQuery(7), CancellationToken.None);
        await sut.GetTrendsAsync(new DashboardQuery(7), CancellationToken.None);
        Assert.Equal(4, cache.FactoryCalls);

        Assert.True(cache.Contains(DashboardCacheKeys.Summary(_teamId, 7)));
        Assert.True(cache.Contains(DashboardCacheKeys.Summary(_teamId, 14)));
        Assert.True(cache.Contains(DashboardCacheKeys.Breakdown(_teamId, 7)));
        Assert.True(cache.Contains(DashboardCacheKeys.Trends(_teamId, 7)));
    }

    [Fact]
    public async Task Cache_NeverMixesTeams()
    {
        var otherTeam = Guid.NewGuid();
        var cache = new InMemoryDashboardCache();
        var sut = CreateService(cache);

        Seed();                                       // tim saya: 1 incident
        Seed(teamId: otherTeam);                      // tim lain: 3 incident
        Seed(teamId: otherTeam);
        Seed(teamId: otherTeam);

        var mine = await sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        _currentUser.TeamId.Returns(otherTeam);       // pemanggil berganti ke tim lain
        var theirs = await sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        _currentUser.TeamId.Returns(_teamId);         // dan kembali
        var mineAgain = await sut.GetSummaryAsync(new DashboardQuery(), CancellationToken.None);

        Assert.Equal(1, mine.OpenIncidents);
        Assert.Equal(3, theirs.OpenIncidents);        // bukan salinan milik tim saya
        Assert.Equal(1, mineAgain.OpenIncidents);
        Assert.Equal(2, cache.FactoryCalls);          // tim saya dibaca dari cache untuk kedua kalinya
    }

    [Fact]
    public async Task Invalidating_OneTeam_ClearsEveryEntryOfThatTeam_AndNoOneElses()
    {
        var otherTeam = Guid.NewGuid();
        var cache = new InMemoryDashboardCache();
        var sut = CreateService(cache);

        foreach (var teamId in new[] { _teamId, otherTeam })
        {
            _currentUser.TeamId.Returns(teamId);
            await sut.GetSummaryAsync(new DashboardQuery(7), CancellationToken.None);
            await sut.GetBreakdownAsync(new DashboardQuery(30), CancellationToken.None);
            await sut.GetTrendsAsync(new DashboardQuery(90), CancellationToken.None);
        }

        await cache.InvalidateTeamAsync(_teamId, CancellationToken.None);

        Assert.False(cache.Contains(DashboardCacheKeys.Summary(_teamId, 7)));
        Assert.False(cache.Contains(DashboardCacheKeys.Breakdown(_teamId, 30)));
        Assert.False(cache.Contains(DashboardCacheKeys.Trends(_teamId, 90)));

        Assert.True(cache.Contains(DashboardCacheKeys.Summary(otherTeam, 7)));
        Assert.True(cache.Contains(DashboardCacheKeys.Breakdown(otherTeam, 30)));
        Assert.True(cache.Contains(DashboardCacheKeys.Trends(otherTeam, 90)));
    }
}