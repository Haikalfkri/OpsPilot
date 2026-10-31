using FluentValidation;
using NSubstitute;
using OpsPilot.Application.Activities;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Tests.Incidents;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Application.Tests.Activities;

public class ActivityServiceTests
{
    private readonly TestDbContext _db = TestDbContext.CreateNew();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly DateTimeOffset _now = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);
    private readonly Guid _myTeamId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly ActivityService _sut;

    public ActivityServiceTests()
    {
        _currentUser.TeamId.Returns(_myTeamId);
        _currentUser.Role.Returns(UserRole.Viewer);   // membaca timeline boleh untuk semua role

        IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string> { [_actorId] = "Demo Responder" };
        _identity
            .GetDisplayNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(names);

        _sut = new ActivityService(new PageQueryValidator(), _db, _identity, _currentUser);
    }

    private async Task<Incident> SeedIncidentAsync(Guid? teamId = null)
    {
        var incident = Incident.Create(
            teamId ?? _myTeamId, _actorId, "Seeded incident", "Seeded description",
            IncidentSeverity.Medium, null, SlaPolicy.Default(), _now);

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
        return incident;
    }

    [Fact]
    public async Task List_ReturnsThisIncidentsActivities_OldestFirst_WithActorNames()
    {
        var incident = await SeedIncidentAsync();
        var other = await SeedIncidentAsync();

        // Sengaja dimasukkan TIDAK berurutan
        _db.IncidentActivities.Add(IncidentActivity.Create(
            incident.Id, _actorId, ActivityType.StatusChanged, "Kedua", null, _now.AddMinutes(2)));
        _db.IncidentActivities.Add(IncidentActivity.Create(
            incident.Id, _actorId, ActivityType.Created, "Pertama", null, _now.AddMinutes(1)));
        _db.IncidentActivities.Add(IncidentActivity.Create(
            other.Id, _actorId, ActivityType.Created, "Milik incident lain", null, _now));
        await _db.SaveChangesAsync();

        var result = await _sut.ListAsync(incident.Id, new PageQuery(), CancellationToken.None);

        Assert.Equal(new[] { "Pertama", "Kedua" }, result.Items.Select(a => a.Message));
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, a => Assert.Equal("Demo Responder", a.ActorName));
    }

    [Fact]
    public async Task List_ShowsSystemAsTheActor_WhenThereIsNoActorId()
    {
        var incident = await SeedIncidentAsync();
        _db.IncidentActivities.Add(IncidentActivity.Create(
            incident.Id, null, ActivityType.ResponseSlaBreached, "Response target breached", null, _now));
        await _db.SaveChangesAsync();

        var result = await _sut.ListAsync(incident.Id, new PageQuery(), CancellationToken.None);

        Assert.Null(result.Items[0].ActorId);
        Assert.Equal("System", result.Items[0].ActorName);
    }

    [Fact]
    public async Task List_Paging_SplitsIntoPages()
    {
        var incident = await SeedIncidentAsync();
        for (var i = 1; i <= 5; i++)
            _db.IncidentActivities.Add(IncidentActivity.Create(
                incident.Id, _actorId, ActivityType.Updated, $"Langkah {i}", null, _now.AddMinutes(i)));
        await _db.SaveChangesAsync();

        var page = await _sut.ListAsync(incident.Id, new PageQuery(Page: 2, PageSize: 2), CancellationToken.None);

        Assert.Equal(new[] { "Langkah 3", "Langkah 4" }, page.Items.Select(a => a.Message));
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPges);
    }

    [Fact]
    public async Task List_OnAnotherTeamsIncident_ThrowsNotFound()
    {
        var foreign = await SeedIncidentAsync(teamId: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.ListAsync(foreign.Id, new PageQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task List_WithInvalidPaging_ThrowsValidation()
    {
        var incident = await SeedIncidentAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.ListAsync(incident.Id, new PageQuery(Page: 0), CancellationToken.None));
    }
}