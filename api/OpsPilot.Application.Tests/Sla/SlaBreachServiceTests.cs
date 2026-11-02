using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Sla;
using OpsPilot.Application.Tests.Incidents;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Application.Tests.Sla;

/// <summary>
/// Semua incident di sini dibuat pada T0 dengan severity Critical bawaan:
/// batas respons T0 + 15 menit, batas penyelesaian T0 + 4 jam.
/// Waktu "sekarang" digeser lewat jam palsu.
/// </summary>
public class SlaBreachServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private readonly TestDbContext _db = TestDbContext.CreateNew();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly MutableTimeProvider _clock = new(T0);
    private readonly SlaBreachService _sut;

    private readonly Guid _teamId = Guid.NewGuid();
    private readonly Guid _admin1 = Guid.NewGuid();
    private readonly Guid _admin2 = Guid.NewGuid();

    public SlaBreachServiceTests()
    {
        GivenAdmins(_teamId, _admin1, _admin2);
        _sut = new SlaBreachService(_db, _identity, _clock);
    }

    // ---------- Helper ----------

    private void GivenAdmins(Guid teamId, params Guid[] adminIds)
    {
        IReadOnlyList<Guid> admins = adminIds;
        _identity.GetActiveAdminIdsAsync(teamId, Arg.Any<CancellationToken>()).Returns(admins);
    }

    private async Task<Incident> SeedAsync(
        Guid? assigneeId = null, Guid? teamId = null, Action<Incident>? configure = null)
    {
        var incident = Incident.Create(
            teamId ?? _teamId, Guid.NewGuid(), "Database is down", "Cannot connect",
            IncidentSeverity.Critical, assigneeId, SlaPolicy.Default(), T0);

        configure?.Invoke(incident);   // mis. memindahkan status sebelum disimpan

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
        return incident;
    }

    /// <summary>Menggeser jam ke "T0 + sekian menit", lalu menjalankan satu siklus.</summary>
    private Task<SlaBreachRunResult> RunAtAsync(int minutesAfterT0)
    {
        _clock.Now = T0.AddMinutes(minutesAfterT0);
        return _sut.DetectAndRecordAsync(CancellationToken.None);
    }

    // ==================== Tidak ada yang perlu dicatat ====================

    [Fact]
    public async Task Run_WithNoIncidents_DoesNothing()
    {
        var result = await RunAtAsync(60);

        Assert.Equal(new SlaBreachRunResult(0, 0, 0), result);
    }

    [Fact]
    public async Task Run_BeforeAnyDueTime_RecordsNothing()
    {
        await SeedAsync();

        var result = await RunAtAsync(10);      // batas respons baru menit ke-15

        Assert.Equal(0, result.BreachesRecorded);
        Assert.Empty(_db.IncidentSlaBreaches);
        Assert.Empty(_db.IncidentActivities);
        Assert.Empty(_db.Notifications);
    }

    // ==================== Pelanggaran respons ====================

    [Fact]
    public async Task Run_AfterTheResponseDueTime_Unassigned_RecordsOneBreach_ASystemActivity_AndNotifiesEveryAdmin()
    {
        var incident = await SeedAsync();

        var result = await RunAtAsync(16);

        Assert.Equal(1, result.BreachesRecorded);
        Assert.Equal(2, result.NotificationsCreated);

        // a) catatan pelanggaran
        var breach = await _db.IncidentSlaBreaches.SingleAsync();
        Assert.Equal(incident.Id, breach.IncidentId);
        Assert.Equal(SlaBreachType.Response, breach.BreachType);
        Assert.Equal(T0.AddMinutes(15), breach.BreachedAtUtc);   // kapan batasnya terlewati
        Assert.Equal(T0.AddMinutes(16), breach.CreatedAtUtc);    // kapan worker mencatatnya

        // b) activity oleh sistem
        var activity = await _db.IncidentActivities.SingleAsync();
        Assert.Equal(ActivityType.ResponseSlaBreached, activity.Type);
        Assert.Null(activity.ActorId);

        // c) notifikasi untuk setiap Admin
        var notifications = await _db.Notifications.ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.RecipientId == _admin1);
        Assert.Contains(notifications, n => n.RecipientId == _admin2);
        Assert.All(notifications, n =>
        {
            Assert.Equal(NotificationType.ResponseSlaBreached, n.Type);
            Assert.Equal(incident.Id, n.IncidentId);
            Assert.False(n.IsRead);
        });
    }

    [Fact]
    public async Task Run_AfterTheResponseDueTime_Assigned_NotifiesOnlyTheAssignee_AndNeverLooksUpAdmins()
    {
        var assigneeId = Guid.NewGuid();
        await SeedAsync(assigneeId: assigneeId);

        var result = await RunAtAsync(16);

        Assert.Equal(1, result.NotificationsCreated);
        Assert.Equal(assigneeId, (await _db.Notifications.SingleAsync()).RecipientId);

        await _identity.DidNotReceiveWithAnyArgs().GetActiveAdminIdsAsync(default, default);
    }

    [Fact]
    public async Task Run_WhenRespondedInTime_NeverRecordsAResponseBreach()
    {
        await SeedAsync(configure: i => i.ChangeStatus(IncidentStatus.Investigating, T0.AddMinutes(5)));

        var result = await RunAtAsync(60);      // jauh setelah batas respons, tapi direspons menit ke-5

        Assert.Equal(0, result.BreachesRecorded);
        Assert.Empty(_db.IncidentSlaBreaches);
    }

    [Fact]
    public async Task Run_WhenRespondedLate_RecordsTheResponseBreach()
    {
        // Direspons menit ke-20, padahal batasnya menit ke-15
        await SeedAsync(configure: i => i.ChangeStatus(IncidentStatus.Investigating, T0.AddMinutes(20)));

        var result = await RunAtAsync(21);

        Assert.Equal(1, result.BreachesRecorded);
        Assert.Equal(SlaBreachType.Response, (await _db.IncidentSlaBreaches.SingleAsync()).BreachType);
    }

    // ==================== Pelanggaran penyelesaian ====================

    [Fact]
    public async Task Run_AfterTheResolutionDueTime_RecordsAResolutionBreach_EvenWhenTheResponseWasOnTime()
    {
        await SeedAsync(configure: i => i.ChangeStatus(IncidentStatus.Investigating, T0.AddMinutes(5)));

        var result = await RunAtAsync(5 * 60);   // 5 jam: batas penyelesaian (4 jam) terlewati

        Assert.Equal(1, result.BreachesRecorded);

        var breach = await _db.IncidentSlaBreaches.SingleAsync();
        Assert.Equal(SlaBreachType.Resolution, breach.BreachType);
        Assert.Equal(T0.AddHours(4), breach.BreachedAtUtc);
        Assert.Equal(ActivityType.ResolutionSlaBreached, (await _db.IncidentActivities.SingleAsync()).Type);
        Assert.Equal(NotificationType.ResolutionSlaBreached, (await _db.Notifications.FirstAsync()).Type);
    }

    [Fact]
    public async Task Run_WhenBothTargetsAreOverdueAtOnce_RecordsBothBreaches()
    {
        await SeedAsync();      // tidak ada yang melakukan apa pun

        var result = await RunAtAsync(5 * 60);

        Assert.Equal(2, result.BreachesRecorded);
        Assert.Equal(2, await _db.IncidentSlaBreaches.CountAsync());
        Assert.Equal(2, await _db.IncidentActivities.CountAsync());
        Assert.Equal(4, result.NotificationsCreated);   // 2 pelanggaran x 2 Admin
    }

    // ==================== Idempotent: inti persyaratan ====================

    [Fact]
    public async Task Run_TwiceInARow_DoesNotDuplicateAnything()
    {
        await SeedAsync();

        var first = await RunAtAsync(16);
        var second = await RunAtAsync(16);

        Assert.Equal(1, first.BreachesRecorded);
        Assert.Equal(0, second.BreachesRecorded);          // siklus kedua tidak menemukan apa pun
        Assert.Equal(0, second.IncidentsChecked);

        Assert.Equal(1, await _db.IncidentSlaBreaches.CountAsync());
        Assert.Equal(1, await _db.IncidentActivities.CountAsync());
        Assert.Equal(2, await _db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Run_RepeatedlyAsTimePasses_RecordsEachTargetExactlyOnce()
    {
        await SeedAsync();

        Assert.Equal(0, (await RunAtAsync(10)).BreachesRecorded);          // belum ada yang lewat
        Assert.Equal(1, (await RunAtAsync(20)).BreachesRecorded);          // respons terlambat
        Assert.Equal(0, (await RunAtAsync(21)).BreachesRecorded);          // sudah dicatat
        Assert.Equal(0, (await RunAtAsync(120)).BreachesRecorded);         // penyelesaian belum lewat
        Assert.Equal(1, (await RunAtAsync(5 * 60)).BreachesRecorded);      // penyelesaian terlambat
        Assert.Equal(0, (await RunAtAsync(6 * 60)).BreachesRecorded);      // keduanya sudah dicatat
        Assert.Equal(0, (await RunAtAsync(24 * 60)).BreachesRecorded);     // dan seterusnya

        Assert.Equal(2, await _db.IncidentSlaBreaches.CountAsync());
        Assert.Equal(2, await _db.IncidentActivities.CountAsync());
    }

    // ==================== Yang tidak dipindai ====================

    [Fact]
    public async Task Run_IgnoresIncidentsThatAreResolvedOrClosed_EvenIfTheyWereLate()
    {
        // Resolved menit ke-20 (batas respons menit ke-15). Statusnya Resolved, jadi tidak dipindai.
        await SeedAsync(configure: i => i.ChangeStatus(IncidentStatus.Resolved, T0.AddMinutes(20)));
        await SeedAsync(configure: i =>
        {
            i.ChangeStatus(IncidentStatus.Resolved, T0.AddMinutes(20));
            i.ChangeStatus(IncidentStatus.Closed, T0.AddMinutes(30));
        });

        var result = await RunAtAsync(10 * 60);

        Assert.Equal(0, result.BreachesRecorded);
        Assert.Empty(_db.IncidentSlaBreaches);
    }

    // ==================== Beberapa tim ====================

    [Fact]
    public async Task Run_NotifiesTheAdminsOfEachIncidentsOwnTeam()
    {
        var otherTeam = Guid.NewGuid();
        var otherAdmin = Guid.NewGuid();
        GivenAdmins(otherTeam, otherAdmin);

        await SeedAsync();                      // tim saya: 2 Admin
        await SeedAsync(teamId: otherTeam);     // tim lain: 1 Admin

        var result = await RunAtAsync(16);

        Assert.Equal(2, result.BreachesRecorded);

        var recipients = await _db.Notifications.Select(n => n.RecipientId).ToListAsync();
        Assert.Equal(3, recipients.Count);
        Assert.Contains(_admin1, recipients);
        Assert.Contains(_admin2, recipients);
        Assert.Contains(otherAdmin, recipients);
    }

    [Fact]
    public async Task Run_WhenTheTeamHasNoActiveAdmin_StillRecordsTheBreachAndTheActivity()
    {
        var emptyTeam = Guid.NewGuid();
        GivenAdmins(emptyTeam);                  // tidak ada Admin
        await SeedAsync(teamId: emptyTeam);

        var result = await RunAtAsync(16);

        Assert.Equal(1, result.BreachesRecorded);
        Assert.Equal(0, result.NotificationsCreated);
        Assert.Single(_db.IncidentActivities);
    }
}