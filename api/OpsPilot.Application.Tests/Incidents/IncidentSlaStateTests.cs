using NSubstitute;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Incidents;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using Xunit;
using static OpsPilot.Application.Incidents.CreateIncidentRequestValidator;

namespace OpsPilot.Application.Tests.Incidents;

/// <summary>
/// Incident dibuat pada T0 dengan severity Critical (respons 15 menit, penyelesaian 4 jam).
/// Setiap tes memilih "sekarang" yang berbeda lewat jam palsu.
/// </summary>
public class IncidentSlaStateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private readonly TestDbContext _db = TestDbContext.CreateNew();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly Guid _teamId = Guid.NewGuid();

    public IncidentSlaStateTests()
    {
        _currentUser.TeamId.Returns(_teamId);
        _currentUser.UserId.Returns(Guid.NewGuid());
        _currentUser.Role.Returns(UserRole.Responder);

        IReadOnlyDictionary<Guid, string> noNames = new Dictionary<Guid, string>();
        _identity
            .GetDisplayNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(noNames);
    }

    // Membuat service dengan jam palsu yang menunjuk ke waktu "now"
    private IncidentService CreateService(DateTimeOffset now) => new(
        new ListIncidentsQueryValidator(),
        new CreateIncidentRequestValidator(),
        new UpdateIncidentRequestValidator(),
        new ChangeStatusRequestValidator(),
        _db,
        _identity,
        _currentUser,
        SlaPolicy.Default(),
        new FixedTimeProvider(now));

    private async Task<Incident> SeedAsync(
        IncidentSeverity severity = IncidentSeverity.Critical,
        string title = "Seeded incident",
        Action<Incident>? configure = null)
    {
        var incident = Incident.Create(
            _teamId, Guid.NewGuid(), title, "Seeded description", severity, null, SlaPolicy.Default(), T0);

        configure?.Invoke(incident);   // mis. memindahkan status sebelum disimpan

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
        return incident;
    }

    // ==================== Detail ====================

    [Fact]
    public async Task GetById_TenMinutesAfterCreation_BothTargetsArePending()
    {
        var incident = await SeedAsync();

        var result = await CreateService(T0.AddMinutes(10)).GetByIdAsync(incident.Id, CancellationToken.None);

        Assert.Equal(SlaState.Pending, result.ResponseSla);
        Assert.Equal(SlaState.Pending, result.ResolutionSla);
    }

    [Fact]
    public async Task GetById_TwentyMinutesAfterCreation_ResponseIsBreached_ResolutionIsPending()
    {
        var incident = await SeedAsync();

        var result = await CreateService(T0.AddMinutes(20)).GetByIdAsync(incident.Id, CancellationToken.None);

        Assert.Equal(SlaState.Breached, result.ResponseSla);
        Assert.Equal(SlaState.Pending, result.ResolutionSla);
    }

    [Fact]
    public async Task GetById_AfterAnInTimeResponse_ResponseIsMet()
    {
        var incident = await SeedAsync(configure: i => i.ChangeStatus(IncidentStatus.Investigating, T0.AddMinutes(5)));

        var result = await CreateService(T0.AddHours(1)).GetByIdAsync(incident.Id, CancellationToken.None);

        Assert.Equal(SlaState.Met, result.ResponseSla);
    }

    [Fact]
    public async Task ChangeStatus_ToResolved_ReturnsAResolutionSlaOfMet()
    {
        var incident = await SeedAsync();

        // Diselesaikan 30 menit setelah dibuat (batas penyelesaian: 4 jam)
        var result = await CreateService(T0.AddMinutes(30)).ChangeStatusAsync(
            incident.Id, new ChangeStatusRequest(IncidentStatus.Resolved), CancellationToken.None);

        Assert.Equal(SlaState.Met, result.ResolutionSla);
        Assert.Equal(SlaState.Breached, result.ResponseSla);   // Resolved di menit ke-30 > batas respons 15 menit
    }

    // ==================== Daftar ====================

    [Fact]
    public async Task List_AssessesEachIncidentWithItsOwnTargets()
    {
        await SeedAsync(IncidentSeverity.Critical, "Critical one");   // batas respons 15 menit
        await SeedAsync(IncidentSeverity.Low, "Low one");             // batas respons 8 jam

        // Satu jam setelah keduanya dibuat
        var result = await CreateService(T0.AddHours(1)).ListAsync(new ListIncidentsQuery(), CancellationToken.None);

        var critical = result.Items.Single(i => i.Title == "Critical one");
        var low = result.Items.Single(i => i.Title == "Low one");

        Assert.Equal(SlaState.Breached, critical.ResponseSla);
        Assert.Equal(SlaState.Pending, low.ResponseSla);
        Assert.Equal(SlaState.Pending, critical.ResolutionSla);
        Assert.Equal(SlaState.Pending, low.ResolutionSla);
    }

    [Fact]
    public async Task List_ShowsTheRespondedAndResolvedTimes_ThatTheSlaIsBasedOn()
    {
        await SeedAsync(title: "Resolved one", configure: i =>
        {
            i.ChangeStatus(IncidentStatus.Investigating, T0.AddMinutes(5));
            i.ChangeStatus(IncidentStatus.Resolved, T0.AddMinutes(50));
        });

        var result = await CreateService(T0.AddHours(2)).ListAsync(new ListIncidentsQuery(), CancellationToken.None);

        var item = result.Items.Single();
        Assert.Equal(T0.AddMinutes(5), item.FirstRespondedAtUtc);
        Assert.Equal(T0.AddMinutes(50), item.ResolvedAtUtc);
        Assert.Equal(SlaState.Met, item.ResponseSla);
        Assert.Equal(SlaState.Met, item.ResolutionSla);
    }
}