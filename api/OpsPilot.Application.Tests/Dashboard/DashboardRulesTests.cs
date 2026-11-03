using OpsPilot.Application.Dashboard;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.Application.Tests.Dashboard;

public class DashboardPeriodTests
{
    [Fact]
    public void Create_SevenDays_StartsAtUtcMidnightSixDaysBeforeToday()
    {
        var now = new DateTimeOffset(2026, 11, 3, 15, 30, 0, TimeSpan.Zero);

        var period = DashboardPeriod.Create(7, now);

        Assert.Equal(7, period.Days);
        Assert.Equal(new DateOnly(2026, 10, 28), period.FromDate);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero), period.FromUtc);
        Assert.Equal(now, period.ToUtc);
    }

    [Fact]
    public void Create_OneDay_IsJustToday()
    {
        var now = new DateTimeOffset(2026, 11, 3, 15, 30, 0, TimeSpan.Zero);

        var period = DashboardPeriod.Create(1, now);

        Assert.Equal(new DateOnly(2026, 11, 3), period.FromDate);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero), period.FromUtc);
    }

    [Fact]
    public void Create_CrossesMonthAndYearBoundaries()
    {
        var now = new DateTimeOffset(2027, 1, 2, 10, 0, 0, TimeSpan.Zero);

        var period = DashboardPeriod.Create(7, now);

        Assert.Equal(new DateOnly(2026, 12, 27), period.FromDate);
    }

    [Fact]
    public void Create_UsesTheUtcDate_EvenWhenTheClockHasAnOffset()
    {
        // 3 Nov 01:00 di UTC+7 = 2 Nov 18:00 UTC. Hari ini (UTC) adalah 2 November.
        var now = new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.FromHours(7));

        var period = DashboardPeriod.Create(1, now);

        Assert.Equal(new DateOnly(2026, 11, 2), period.FromDate);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero), period.FromUtc);
    }
}

public class DashboardQueryValidatorTests
{
    private readonly DashboardQueryValidator _validator = new();

    [Fact]
    public void Validate_WithTheDefault_IsValid() =>
        Assert.True(_validator.Validate(new DashboardQuery()).IsValid);

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(30)]
    [InlineData(90)]
    public void Validate_WithAnAllowedPeriod_IsValid(int days) =>
        Assert.True(_validator.Validate(new DashboardQuery(days)).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(365)]
    [InlineData(-7)]
    public void Validate_WithAnyOtherNumber_IsInvalid(int days) =>
        Assert.False(_validator.Validate(new DashboardQuery(days)).IsValid);
}

public class DashboardCacheKeysTests
{
    private readonly Guid _teamA = Guid.NewGuid();
    private readonly Guid _teamB = Guid.NewGuid();

    [Fact]
    public void AllForTeam_ReturnsOneKeyPerEndpointPerPeriod_AllDistinct()
    {
        var keys = DashboardCacheKeys.AllForTeam(_teamA);

        Assert.Equal(12, keys.Count);                       // 3 jenis x 4 periode
        Assert.Equal(12, keys.Distinct().Count());
    }

    [Fact]
    public void AllForTeam_ContainsEveryKeyTheServiceCanUse()
    {
        var keys = DashboardCacheKeys.AllForTeam(_teamA);

        foreach (var days in DashboardPeriods.Allowed)
        {
            Assert.Contains(DashboardCacheKeys.Summary(_teamA, days), keys);
            Assert.Contains(DashboardCacheKeys.Breakdown(_teamA, days), keys);
            Assert.Contains(DashboardCacheKeys.Trends(_teamA, days), keys);
        }
    }

    [Fact]
    public void Keys_ContainTheTeamId_SoTeamsNeverShareAnEntry()
    {
        Assert.All(DashboardCacheKeys.AllForTeam(_teamA), key => Assert.Contains(_teamA.ToString(), key));
        Assert.Empty(DashboardCacheKeys.AllForTeam(_teamA).Intersect(DashboardCacheKeys.AllForTeam(_teamB)));
    }

    [Fact]
    public void Keys_DifferByPeriodAndByKind()
    {
        Assert.NotEqual(DashboardCacheKeys.Summary(_teamA, 7), DashboardCacheKeys.Summary(_teamA, 14));
        Assert.NotEqual(DashboardCacheKeys.Summary(_teamA, 7), DashboardCacheKeys.Breakdown(_teamA, 7));
        Assert.NotEqual(DashboardCacheKeys.Breakdown(_teamA, 7), DashboardCacheKeys.Trends(_teamA, 7));
    }
}

public class DashboardMapperTests
{
    [Fact]
    public void ToSeverityCounts_ListsEverySeverity_HighestFirst_FillingZeros()
    {
        var counts = new Dictionary<IncidentSeverity, int>
        {
            [IncidentSeverity.Low] = 4,
            [IncidentSeverity.Critical] = 1
        };

        var result = DashboardMapper.ToSeverityCounts(counts);

        Assert.Equal(
            new[] { IncidentSeverity.Critical, IncidentSeverity.High, IncidentSeverity.Medium, IncidentSeverity.Low },
            result.Select(r => r.Severity).ToArray());
        Assert.Equal(new[] { 1, 0, 0, 4 }, result.Select(r => r.Count).ToArray());
    }

    [Fact]
    public void ToStatusCounts_ListsEveryStatus_InLifecycleOrder_FillingZeros()
    {
        var counts = new Dictionary<IncidentStatus, int> { [IncidentStatus.Closed] = 2 };

        var result = DashboardMapper.ToStatusCounts(counts);

        Assert.Equal(
            new[] { IncidentStatus.Open, IncidentStatus.Investigating, IncidentStatus.Resolved, IncidentStatus.Closed },
            result.Select(r => r.Status).ToArray());
        Assert.Equal(new[] { 0, 0, 0, 2 }, result.Select(r => r.Count).ToArray());
    }

    [Fact]
    public void ToTrendPoints_ReturnsExactlyOnePointPerDay_OldestFirst()
    {
        var period = DashboardPeriod.Create(7, new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));

        var points = DashboardMapper.ToTrendPoints(period, Array.Empty<DateTimeOffset>(), Array.Empty<DateTimeOffset>());

        var expectedDates = Enumerable.Range(0, 7).Select(i => new DateOnly(2026, 10, 24).AddDays(i)).ToArray();
        Assert.Equal<DateOnly>(expectedDates, points.Select(p => p.Date).ToArray());
        Assert.All(points, p =>
        {
            Assert.Equal(0, p.Created);
            Assert.Equal(0, p.Resolved);
        });
    }

    [Fact]
    public void ToTrendPoints_PlacesEachTimestampOnItsUtcDay_IncludingTheMidnightBoundary()
    {
        var period = DashboardPeriod.Create(3, new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));   // 28, 29, 30 Okt

        var created = new[]
        {
            new DateTimeOffset(2026, 10, 29, 23, 59, 59, TimeSpan.Zero),   // masih tanggal 29
            new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero),      // tepat tengah malam: sudah tanggal 30
            new DateTimeOffset(2026, 10, 30, 7, 0, 0, TimeSpan.Zero)
        };
        var resolved = new[] { new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero) };

        var points = DashboardMapper.ToTrendPoints(period, created, resolved);

        Assert.Equal(new[] { 0, 1, 2 }, points.Select(p => p.Created).ToArray());
        Assert.Equal(new[] { 1, 0, 0 }, points.Select(p => p.Resolved).ToArray());
    }
}