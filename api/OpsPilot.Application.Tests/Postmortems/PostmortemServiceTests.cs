using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Postmortems;
using OpsPilot.Application.Tests.Incidents;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Application.Tests.Postmortems;

public class PostmortemServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private readonly TestDbContext _db = TestDbContext.CreateNew();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly MutableTimeProvider _clock = new(T0);

    private readonly Guid _myTeamId = Guid.NewGuid();
    private readonly Guid _responderId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly PostmortemService _sut;

    public PostmortemServiceTests()
    {
        _currentUser.TeamId.Returns(_myTeamId);
        ActAs(_responderId, UserRole.Responder);       // bawaan: pemanggil adalah Responder

        IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>
        {
            [_responderId] = "Demo Responder",
            [_adminId] = "Demo Admin"
        };
        _identity
            .GetDisplayNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(names);

        _sut = new PostmortemService(new SavePostmortemRequestValidator(), _db, _identity, _currentUser, _clock);
    }

    // ---------- Helper ----------

    private void ActAs(Guid userId, UserRole role)
    {
        _currentUser.UserId.Returns(userId);
        _currentUser.Role.Returns(role);
    }

    private async Task<Incident> SeedIncidentAsync(IncidentStatus status = IncidentStatus.Resolved, Guid? teamId = null)
    {
        var incident = Incident.Create(
            teamId ?? _myTeamId, _responderId, "Seeded incident", "Seeded description",
            IncidentSeverity.Medium, null, SlaPolicy.Default(), T0);

        incident.Status = status;

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
        return incident;
    }

    private static SavePostmortemRequest ValidRequest() =>
        new("Summary text", "Root cause text", "Impact text", "Corrective actions text");

    // ==================== Save: siapa yang boleh ====================

    [Fact]
    public async Task Save_AsViewer_ThrowsForbidden_AndSavesNothing()
    {
        var incident = await SeedIncidentAsync();
        ActAs(Guid.NewGuid(), UserRole.Viewer);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.SaveAsync(incident.Id, ValidRequest(), CancellationToken.None));

        Assert.Empty(_db.Postmortems);
        Assert.Empty(_db.IncidentActivities);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Responder)]
    public async Task Save_AsAdminOrResponder_Works(UserRole role)
    {
        var incident = await SeedIncidentAsync();
        ActAs(_adminId, role);

        var result = await _sut.SaveAsync(incident.Id, ValidRequest(), CancellationToken.None);

        Assert.True(result.Created);
    }

    // ==================== Save: validasi dan tim ====================

    [Fact]
    public async Task Save_WithAnInvalidRequest_ThrowsValidation_AndSavesNothing()
    {
        var incident = await SeedIncidentAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.SaveAsync(incident.Id, ValidRequest() with { Summary = "" }, CancellationToken.None));

        Assert.Empty(_db.Postmortems);
    }

    [Fact]
    public async Task Save_OnAnotherTeamsIncident_ThrowsNotFound_AndSavesNothing()
    {
        var foreign = await SeedIncidentAsync(teamId: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.SaveAsync(foreign.Id, ValidRequest(), CancellationToken.None));

        Assert.Empty(_db.Postmortems);
        Assert.Empty(_db.IncidentActivities);
    }

    [Fact]
    public async Task Save_OnAnUnknownIncident_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.SaveAsync(Guid.NewGuid(), ValidRequest(), CancellationToken.None));
    }

    // ==================== Save: aturan status ====================

    [Theory]
    [InlineData(IncidentStatus.Open)]
    [InlineData(IncidentStatus.Investigating)]
    public async Task Save_OnAnIncidentThatIsNotResolvedYet_ThrowsDomainRule_AndSavesNothing(IncidentStatus status)
    {
        var incident = await SeedIncidentAsync(status);

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _sut.SaveAsync(incident.Id, ValidRequest(), CancellationToken.None));

        Assert.Empty(_db.Postmortems);
        Assert.Empty(_db.IncidentActivities);
    }

    [Theory]
    [InlineData(IncidentStatus.Resolved)]
    [InlineData(IncidentStatus.Closed)]
    public async Task Save_OnAResolvedOrClosedIncident_Works(IncidentStatus status)
    {
        var incident = await SeedIncidentAsync(status);

        var result = await _sut.SaveAsync(incident.Id, ValidRequest(), CancellationToken.None);

        Assert.True(result.Created);
    }

    // ==================== Save: membuat ====================

    [Fact]
    public async Task Save_TheFirstTime_CreatesThePostmortem_WithTheAuthorFromTheToken_AndACreatedActivity()
    {
        var incident = await SeedIncidentAsync();
        _clock.Now = T0.AddMinutes(30);

        var result = await _sut.SaveAsync(
            incident.Id, new SavePostmortemRequest("  Summary  ", " Root ", " Impact ", " Actions "), CancellationToken.None);

        Assert.True(result.Created);
        Assert.Equal("Demo Responder", result.Postmortem.AuthorName);

        var saved = await _db.Postmortems.SingleAsync();
        Assert.Equal(incident.Id, saved.IncidentId);
        Assert.Equal(_responderId, saved.AuthorId);                  // penulis dari token
        Assert.Equal("Summary", saved.Summary);                      // spasi di ujung dibuang
        Assert.Equal("Root", saved.RootCause);
        Assert.Equal("Impact", saved.Impact);
        Assert.Equal("Actions", saved.CorrectiveActions);
        Assert.Equal(T0.AddMinutes(30), saved.CreatedAtUtc);
        Assert.Equal(saved.CreatedAtUtc, saved.UpdatedAtUtc);

        var activity = await _db.IncidentActivities.SingleAsync();
        Assert.Equal(ActivityType.PostmortemCreated, activity.Type);
        Assert.Equal(incident.Id, activity.IncidentId);
        Assert.Equal(_responderId, activity.ActorId);
        Assert.Contains(saved.Id.ToString(), activity.MetadataJson);   // activity menunjuk ke postmortem-nya
    }

    [Fact]
    public async Task Save_DoesNotChangeTheIncidentItself()
    {
        var incident = await SeedIncidentAsync();
        var updatedBefore = incident.UpdatedAtUtc;

        await _sut.SaveAsync(incident.Id, ValidRequest(), CancellationToken.None);

        var after = await _db.Incidents.AsNoTracking().SingleAsync();
        Assert.Equal(updatedBefore, after.UpdatedAtUtc);
        Assert.Equal(IncidentStatus.Resolved, after.Status);
    }

    // ==================== Save: memperbarui ====================

    [Fact]
    public async Task Save_AgainWithChanges_UpdatesTheSamePostmortem_KeepingTheOriginalAuthorAndCreationTime()
    {
        var incident = await SeedIncidentAsync();

        _clock.Now = T0.AddMinutes(30);
        await _sut.SaveAsync(incident.Id, ValidRequest(), CancellationToken.None);       // dibuat oleh Responder

        ActAs(_adminId, UserRole.Admin);                                                  // lalu diperbarui oleh Admin
        _clock.Now = T0.AddHours(2);
        var result = await _sut.SaveAsync(
            incident.Id, ValidRequest() with { RootCause = "Disk penuh" }, CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal("Disk penuh", result.Postmortem.RootCause);
        Assert.Equal("Demo Responder", result.Postmortem.AuthorName);                     // penulis asli tetap

        var saved = await _db.Postmortems.SingleAsync();                                  // masih hanya satu
        Assert.Equal(_responderId, saved.AuthorId);
        Assert.Equal(T0.AddMinutes(30), saved.CreatedAtUtc);
        Assert.Equal(T0.AddHours(2), saved.UpdatedAtUtc);
        Assert.Equal("Disk penuh", saved.RootCause);

        // Timeline: satu "created" (oleh Responder) dan satu "updated" (oleh Admin)
        var activities = await _db.IncidentActivities.OrderBy(a => a.CreatedAtUtc).ToListAsync();
        Assert.Equal(new[] { ActivityType.PostmortemCreated, ActivityType.PostmortemUpdated },
                     activities.Select(a => a.Type).ToArray());
        Assert.Equal(_responderId, activities[0].ActorId);
        Assert.Equal(_adminId, activities[1].ActorId);
    }

    [Fact]
    public async Task Save_AgainWithTheSameContent_ChangesNothing_AndRecordsNoNewActivity()
    {
        var incident = await SeedIncidentAsync();

        _clock.Now = T0.AddMinutes(30);
        await _sut.SaveAsync(incident.Id, ValidRequest(), CancellationToken.None);

        _clock.Now = T0.AddHours(5);
        var result = await _sut.SaveAsync(incident.Id, ValidRequest(), CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal(T0.AddMinutes(30), (await _db.Postmortems.SingleAsync()).UpdatedAtUtc);   // tidak bergeser
        Assert.Single(_db.IncidentActivities);                                                 // hanya "created"
    }

    [Fact]
    public async Task Save_AgainWithOnlyDifferentSpacing_CountsAsTheSameContent()
    {
        var incident = await SeedIncidentAsync();
        await _sut.SaveAsync(incident.Id, ValidRequest(), CancellationToken.None);

        var withSpaces = new SavePostmortemRequest("  Summary text ", "Root cause text  ", " Impact text", "Corrective actions text ");
        await _sut.SaveAsync(incident.Id, withSpaces, CancellationToken.None);

        Assert.Single(_db.IncidentActivities);
    }

    [Fact]
    public async Task EachIncident_HasItsOwnPostmortem()
    {
        var first = await SeedIncidentAsync();
        var second = await SeedIncidentAsync();

        await _sut.SaveAsync(first.Id, ValidRequest() with { Summary = "First" }, CancellationToken.None);
        await _sut.SaveAsync(second.Id, ValidRequest() with { Summary = "Second" }, CancellationToken.None);

        Assert.Equal(2, await _db.Postmortems.CountAsync());
        Assert.Equal("First", (await _sut.GetAsync(first.Id, CancellationToken.None)).Summary);
        Assert.Equal("Second", (await _sut.GetAsync(second.Id, CancellationToken.None)).Summary);
    }

    // ==================== Get ====================

    [Fact]
    public async Task Get_ReturnsThePostmortem_WithTheAuthorName()
    {
        var incident = await SeedIncidentAsync();
        await _sut.SaveAsync(incident.Id, ValidRequest(), CancellationToken.None);

        var result = await _sut.GetAsync(incident.Id, CancellationToken.None);

        Assert.Equal(incident.Id, result.IncidentId);
        Assert.Equal("Summary text", result.Summary);
        Assert.Equal("Demo Responder", result.AuthorName);
    }

    [Fact]
    public async Task Get_AsViewer_IsAllowed()
    {
        var incident = await SeedIncidentAsync();
        await _sut.SaveAsync(incident.Id, ValidRequest(), CancellationToken.None);
        ActAs(Guid.NewGuid(), UserRole.Viewer);

        var result = await _sut.GetAsync(incident.Id, CancellationToken.None);

        Assert.Equal("Summary text", result.Summary);
    }

    [Fact]
    public async Task Get_WhenNoPostmortemExistsYet_ThrowsNotFound()
    {
        var incident = await SeedIncidentAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetAsync(incident.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Get_OnAnotherTeamsIncident_ThrowsNotFound_EvenThoughAPostmortemExists()
    {
        var foreign = await SeedIncidentAsync(teamId: Guid.NewGuid());
        _db.Postmortems.Add(Postmortem.Create(foreign.Id, _responderId, "S", "R", "I", "C", T0));
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetAsync(foreign.Id, CancellationToken.None));
    }
}