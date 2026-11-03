using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class DashboardEndpointTests(ApiFactory factory)
{
    private const string SummaryUrl = "/api/v1/dashboard/summary";
    private const string BreakdownUrl = "/api/v1/dashboard/incident-breakdown";
    private const string TrendsUrl = "/api/v1/dashboard/trends";

    // ==================== Helper ====================

    private static Task<JsonElement> GetAsync(HttpClient client, string url) =>
        client.GetFromJsonAsync<JsonElement>(url);

    /// <summary>Mengubah daftar {severity/status, count} menjadi kamus supaya mudah diperiksa.</summary>
    private static Dictionary<string, int> ToCounts(JsonElement array, string keyName) =>
        array.EnumerateArray().ToDictionary(
            item => item.GetProperty(keyName).GetString()!,
            item => item.GetProperty("count").GetInt32());

    /// <summary>Mengatur waktu dibuat dan waktu selesai langsung di database (supaya tidak perlu menunggu berhari-hari).</summary>
    private async Task SetTimesAsync(Guid incidentId, DateTimeOffset createdAt, DateTimeOffset resolvedAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpsPilotDbContext>();

        await db.Incidents.Where(i => i.Id == incidentId).ExecuteUpdateAsync(s => s
            .SetProperty(i => i.CreatedAtUtc, createdAt)
            .SetProperty(i => i.ResolvedAtUtc, (DateTimeOffset?)resolvedAt));
    }

    /// <summary>Menggeser batas waktu SLA ke masa lalu, seolah-olah incident sudah lama berjalan.</summary>
    private async Task MakeDueInThePastAsync(Guid incidentId, bool response, bool resolution)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpsPilotDbContext>();

        var past = DateTimeOffset.UtcNow.AddHours(-1);
        var query = db.Incidents.Where(i => i.Id == incidentId);

        if (response)
            await query.ExecuteUpdateAsync(s => s.SetProperty(i => i.ResponseDueAtUtc, past));
        if (resolution)
            await query.ExecuteUpdateAsync(s => s.SetProperty(i => i.ResolutionDueAtUtc, past));
    }

    // ==================== 401, role, dan validasi ====================

    [Fact]
    public async Task EveryEndpoint_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        foreach (var url in new[] { SummaryUrl, BreakdownUrl, TrendsUrl })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData(TestUsers.Admin)]
    [InlineData(TestUsers.Responder)]
    [InlineData(TestUsers.Viewer)]
    public async Task EveryRole_CanReadEveryDashboardEndpoint(string email)
    {
        var client = await factory.CreateAuthenticatedClientAsync(email);

        foreach (var url in new[] { SummaryUrl, BreakdownUrl, TrendsUrl })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(365)]
    [InlineData(-7)]
    public async Task EveryEndpoint_WithAnUnsupportedPeriod_Returns400WithAFieldError(int days)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Viewer);

        foreach (var url in new[] { SummaryUrl, BreakdownUrl, TrendsUrl })
        {
            var response = await client.GetAsync($"{url}?days={days}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errors = (await response.ReadJsonAsync()).GetProperty("errors");
            Assert.True(errors.TryGetProperty("days", out _));
        }
    }

    // ==================== Tim kosong ====================

    [Fact]
    public async Task ForAnEmptyTeam_EveryEndpoint_ReturnsValidZeroValues_NotAnError()
    {
        var admin = await TestTeams.CreateAdminClientAsync(factory);

        var summary = await GetAsync(admin, SummaryUrl);
        Assert.Equal(0, summary.GetProperty("openIncidents").GetInt32());
        Assert.Equal(0, summary.GetProperty("criticalHighOpenIncidents").GetInt32());
        Assert.Equal(0, summary.GetProperty("slaBreachedIncidents").GetInt32());
        Assert.Equal(0, summary.GetProperty("resolvedInPeriod").GetInt32());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("averageResolutionMinutes").ValueKind);
        Assert.Equal(7, summary.GetProperty("period").GetProperty("days").GetInt32());

        var breakdown = await GetAsync(admin, BreakdownUrl);
        Assert.Equal(4, breakdown.GetProperty("bySeverity").GetArrayLength());
        Assert.Equal(4, breakdown.GetProperty("byStatus").GetArrayLength());
        Assert.All(ToCounts(breakdown.GetProperty("bySeverity"), "severity").Values, count => Assert.Equal(0, count));
        Assert.All(ToCounts(breakdown.GetProperty("byStatus"), "status").Values, count => Assert.Equal(0, count));

        var trends = await GetAsync(admin, TrendsUrl);
        var points = trends.GetProperty("points").EnumerateArray().ToList();
        Assert.Equal(7, points.Count);
        Assert.All(points, p =>
        {
            Assert.Equal(0, p.GetProperty("created").GetInt32());
            Assert.Equal(0, p.GetProperty("resolved").GetInt32());
        });
    }

    [Fact]
    public async Task TheBreakdown_AlwaysListsSeveritiesAndStatuses_InAFixedOrder()
    {
        var admin = await TestTeams.CreateAdminClientAsync(factory);

        var breakdown = await GetAsync(admin, BreakdownUrl);

        Assert.Equal(
            new[] { "Critical", "High", "Medium", "Low" },
            breakdown.GetProperty("bySeverity").EnumerateArray().Select(i => i.GetProperty("severity").GetString()));
        Assert.Equal(
            new[] { "Open", "Investigating", "Resolved", "Closed" },
            breakdown.GetProperty("byStatus").EnumerateArray().Select(i => i.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task WithoutADaysParameter_ThePeriodDefaultsToSevenDays_EndingNow()
    {
        var admin = await TestTeams.CreateAdminClientAsync(factory);

        var summary = await GetAsync(admin, SummaryUrl);

        var period = summary.GetProperty("period");
        var toUtc = period.GetProperty("toUtc").GetDateTimeOffset();
        var fromUtc = period.GetProperty("fromUtc").GetDateTimeOffset();

        Assert.Equal(7, period.GetProperty("days").GetInt32());
        Assert.True(DateTimeOffset.UtcNow - toUtc < TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.Zero, fromUtc.TimeOfDay);                         // awal periode = tengah malam UTC
        Assert.InRange((toUtc - fromUtc).TotalDays, 6.0, 7.0);                  // 6 hari penuh + hari ini
    }

    // ==================== Angka mengikuti data ====================

    [Fact]
    public async Task Counts_FollowTheRealLifecycleOfIncidents()
    {
        var admin = await TestTeams.CreateAdminClientAsync(factory);

        await TestIncidents.CreateAsync(admin, "Critical");                                   // tetap Open
        var high = await TestIncidents.CreateAsync(admin, "High");
        await TestIncidents.ChangeStatusAsync(admin, high, "Investigating");                  // Investigating
        var low = await TestIncidents.CreateAsync(admin, "Low");
        await TestIncidents.ChangeStatusAsync(admin, low, "Resolved");                        // Resolved
        var medium = await TestIncidents.CreateAsync(admin, "Medium");
        await TestIncidents.ChangeStatusAsync(admin, medium, "Resolved");
        await TestIncidents.ChangeStatusAsync(admin, medium, "Closed");                       // Closed

        var summary = await GetAsync(admin, SummaryUrl);
        Assert.Equal(2, summary.GetProperty("openIncidents").GetInt32());                     // Open + Investigating
        Assert.Equal(2, summary.GetProperty("criticalHighOpenIncidents").GetInt32());
        Assert.Equal(0, summary.GetProperty("slaBreachedIncidents").GetInt32());              // semuanya masih baru
        Assert.Equal(2, summary.GetProperty("resolvedInPeriod").GetInt32());                  // Resolved + Closed
        Assert.InRange(summary.GetProperty("averageResolutionMinutes").GetDouble(), 0, 5);    // diselesaikan dalam hitungan detik

        var breakdown = await GetAsync(admin, BreakdownUrl);
        var bySeverity = ToCounts(breakdown.GetProperty("bySeverity"), "severity");
        var byStatus = ToCounts(breakdown.GetProperty("byStatus"), "status");

        Assert.Equal(1, bySeverity["Critical"]);
        Assert.Equal(1, bySeverity["High"]);
        Assert.Equal(1, bySeverity["Medium"]);
        Assert.Equal(1, bySeverity["Low"]);
        Assert.Equal(1, byStatus["Open"]);
        Assert.Equal(1, byStatus["Investigating"]);
        Assert.Equal(1, byStatus["Resolved"]);
        Assert.Equal(1, byStatus["Closed"]);

        // Dijumlahkan seluruh hari (supaya tidak goyah kalau tes berjalan tepat melewati tengah malam UTC)
        var trends = await GetAsync(admin, TrendsUrl);
        var points = trends.GetProperty("points").EnumerateArray().ToList();
        Assert.Equal(4, points.Sum(p => p.GetProperty("created").GetInt32()));
        Assert.Equal(2, points.Sum(p => p.GetProperty("resolved").GetInt32()));
    }

    [Fact]
    public async Task ANewIncident_ShowsUpOnTheNextRequest()
    {
        var admin = await TestTeams.CreateAdminClientAsync(factory);

        var before = await GetAsync(admin, SummaryUrl);
        await TestIncidents.CreateAsync(admin, "High");
        var after = await GetAsync(admin, SummaryUrl);

        Assert.Equal(0, before.GetProperty("openIncidents").GetInt32());
        Assert.Equal(1, after.GetProperty("openIncidents").GetInt32());
    }

    // ==================== Melanggar SLA ====================

    [Fact]
    public async Task SlaBreached_CountsAnOverdueActiveIncident_AndStopsCountingItOnceItIsResolved()
    {
        var admin = await TestTeams.CreateAdminClientAsync(factory);
        var id = await TestIncidents.CreateAsync(admin, "High");

        Assert.Equal(0, (await GetAsync(admin, SummaryUrl)).GetProperty("slaBreachedIncidents").GetInt32());

        await MakeDueInThePastAsync(id, response: true, resolution: false);
        Assert.Equal(1, (await GetAsync(admin, SummaryUrl)).GetProperty("slaBreachedIncidents").GetInt32());

        await TestIncidents.ChangeStatusAsync(admin, id, "Resolved");
        Assert.Equal(0, (await GetAsync(admin, SummaryUrl)).GetProperty("slaBreachedIncidents").GetInt32());
    }

    [Fact]
    public async Task SlaBreached_AlsoCountsAnIncidentThatWasRespondedInTime_ButIsPastItsResolutionTarget()
    {
        var admin = await TestTeams.CreateAdminClientAsync(factory);
        var id = await TestIncidents.CreateAsync(admin, "High");
        await TestIncidents.ChangeStatusAsync(admin, id, "Investigating");       // respons tepat waktu

        await MakeDueInThePastAsync(id, response: false, resolution: true);

        Assert.Equal(1, (await GetAsync(admin, SummaryUrl)).GetProperty("slaBreachedIncidents").GetInt32());
    }

    // ==================== Periode ====================

    [Fact]
    public async Task ALongerPeriod_IncludesOlderResolvedIncidents_AndComputesTheAverageFromRealTimes()
    {
        var admin = await TestTeams.CreateAdminClientAsync(factory);
        var id = await TestIncidents.CreateAsync(admin, "Medium");
        await TestIncidents.ChangeStatusAsync(admin, id, "Resolved");

        // Dibuat 10 hari lalu, diselesaikan 90 menit kemudian
        var created = DateTimeOffset.UtcNow.AddDays(-10);
        await SetTimesAsync(id, created, created.AddMinutes(90));

        var week = await GetAsync(admin, $"{SummaryUrl}?days=7");
        var fortnight = await GetAsync(admin, $"{SummaryUrl}?days=14");

        Assert.Equal(0, week.GetProperty("resolvedInPeriod").GetInt32());
        Assert.Equal(JsonValueKind.Null, week.GetProperty("averageResolutionMinutes").ValueKind);

        Assert.Equal(1, fortnight.GetProperty("resolvedInPeriod").GetInt32());
        Assert.Equal(90.0, fortnight.GetProperty("averageResolutionMinutes").GetDouble(), precision: 1);
    }

    [Fact]
    public async Task Trends_AlwaysHaveExactlyOnePointPerDayOfThePeriod()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Viewer);

        foreach (var days in new[] { 7, 14, 30, 90 })
        {
            var trends = await GetAsync(client, $"{TrendsUrl}?days={days}");
            var dates = trends.GetProperty("points").EnumerateArray()
                .Select(p => DateOnly.Parse(p.GetProperty("date").GetString()!))
                .ToList();

            Assert.Equal(days, dates.Count);
            Assert.Equal(dates.OrderBy(d => d), dates);                                          // terlama ke terbaru
            Assert.Equal(days - 1, dates.Last().DayNumber - dates.First().DayNumber);            // berurutan, tanpa bolong
        }
    }

    // ==================== Isolasi tim ====================

    [Fact]
    public async Task EachTeam_OnlySeesItsOwnNumbers()
    {
        var teamA = await TestTeams.CreateAdminClientAsync(factory);
        var teamB = await TestTeams.CreateAdminClientAsync(factory);

        await TestIncidents.CreateAsync(teamA, "Critical");
        await TestIncidents.CreateAsync(teamA, "High");

        var a = await GetAsync(teamA, SummaryUrl);
        var b = await GetAsync(teamB, SummaryUrl);

        Assert.Equal(2, a.GetProperty("openIncidents").GetInt32());
        Assert.Equal(0, b.GetProperty("openIncidents").GetInt32());     // tim B tidak melihat milik tim A

        var breakdownB = await GetAsync(teamB, BreakdownUrl);
        Assert.All(ToCounts(breakdownB.GetProperty("bySeverity"), "severity").Values, count => Assert.Equal(0, count));
    }
}