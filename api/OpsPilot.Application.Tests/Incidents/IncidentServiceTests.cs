using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Incidents;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;
using OpsPilot.Domain.Sla;
using Xunit;
using static OpsPilot.Application.Incidents.CreateIncidentRequestValidator;

namespace OpsPilot.Application.Tests.Incidents;

public class IncidentServiceTests
{
    private readonly TestDbContext _db = TestDbContext.CreateNew();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly DateTimeOffset _now = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);
    private readonly Guid _myTeamId = Guid.NewGuid();
    private readonly Guid _myUserId = Guid.NewGuid();
    private readonly IncidentService _sut;

    public IncidentServiceTests()
    {
        _currentUser.TeamId.Returns(_myTeamId);
        _currentUser.UserId.Returns(_myUserId);
        _currentUser.Role.Returns(UserRole.Responder);   // bawaan: pemanggil adalah Responder

        // Pencarian nama user: bawaan kosong (nama tidak penting untuk kebanyakan tes)
        IReadOnlyDictionary<Guid, string> noNames = new Dictionary<Guid, string>();
        _identity
            .GetDisplayNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(noNames);

        _sut = new IncidentService(
            new ListIncidentsQueryValidator(),
            new CreateIncidentRequestValidator(),
            new UpdateIncidentRequestValidator(),
            new ChangeStatusRequestValidator(),
            _db,
            _identity,
            _currentUser,
            SlaPolicy.Default(),
            new FixedTimeProvider(_now));
    }

    // ---------- Helper ----------

    private async Task<Incident> SeedIncidentAsync(
        Guid? teamId = null, IncidentSeverity severity = IncidentSeverity.Medium, string title = "Seeded incident")
    {
        var incident = Incident.Create(
            teamId ?? _myTeamId, _myUserId, title, "Seeded description", severity, null, SlaPolicy.Default(), _now);

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
        return incident;
    }

    private void GivenAssignee(Guid id, UserRole role = UserRole.Responder, bool isActive = true, Guid? teamId = null) =>
        _identity
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(new UserInfo(id, "Assignee", "assignee@opspilot.local", role, teamId ?? _myTeamId, isActive));

    private static CreateIncidentRequest ValidCreate(IncidentSeverity severity = IncidentSeverity.Critical, Guid? assigneeId = null) =>
        new("Database is down", "Cannot connect to the primary database", severity, assigneeId);

    private Task<List<IncidentActivity>> ActivitiesOfAsync(Guid incidentId) =>
        _db.IncidentActivities.Where(a => a.IncidentId == incidentId).ToListAsync();

    // ==================== Create ====================

    [Fact]
    public async Task Create_AsViewer_ThrowsForbidden_AndSavesNothing()
    {
        _currentUser.Role.Returns(UserRole.Viewer);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.CreateAsync(ValidCreate(), CancellationToken.None));

        Assert.Empty(_db.Incidents);
    }

    [Fact]
    public async Task Create_WithInvalidRequest_ThrowsValidation_AndSavesNothing()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateAsync(ValidCreate() with { Title = "abc" }, CancellationToken.None));

        Assert.Empty(_db.Incidents);
    }

    [Fact]
    public async Task Create_Valid_StoresAnOpenIncident_InMyTeam_WithSlaAndACreatedActivity()
    {
        var result = await _sut.CreateAsync(ValidCreate(IncidentSeverity.Critical), CancellationToken.None);

        var saved = await _db.Incidents.SingleAsync();
        Assert.Equal(result.Id, saved.Id);
        Assert.Equal(_myTeamId, saved.TeamId);              // tim dari token
        Assert.Equal(_myUserId, saved.CreatedById);         // pembuat dari token
        Assert.Equal(IncidentStatus.Open, saved.Status);
        Assert.Equal(_now.AddMinutes(15), saved.ResponseDueAtUtc);
        Assert.Equal(_now.AddHours(4), saved.ResolutionDueAtUtc);

        var activities = await ActivitiesOfAsync(saved.Id);
        Assert.Single(activities);
        Assert.Equal(ActivityType.Created, activities[0].Type);
        Assert.Equal(_myUserId, activities[0].ActorId);
    }

    [Fact]
    public async Task Create_WithValidAssignee_AlsoRecordsAnAssigneeActivity()
    {
        var assigneeId = Guid.NewGuid();
        GivenAssignee(assigneeId);

        var result = await _sut.CreateAsync(ValidCreate(assigneeId: assigneeId), CancellationToken.None);

        Assert.Equal(assigneeId, result.AssigneeId);

        var activities = await ActivitiesOfAsync(result.Id);
        Assert.Equal(2, activities.Count);
        Assert.Contains(activities, a => a.Type == ActivityType.AssigneeChanged);
    }

    [Theory]
    [InlineData("viewer")]
    [InlineData("inactive")]
    [InlineData("otherTeam")]
    [InlineData("missing")]
    public async Task Create_WithInvalidAssignee_ThrowsValidation_AndSavesNothing(string scenario)
    {
        var assigneeId = Guid.NewGuid();

        switch (scenario)
        {
            case "viewer": GivenAssignee(assigneeId, role: UserRole.Viewer); break;
            case "inactive": GivenAssignee(assigneeId, isActive: false); break;
            case "otherTeam": GivenAssignee(assigneeId, teamId: Guid.NewGuid()); break;
            case "missing":
                _identity.GetByIdAsync(assigneeId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<UserInfo?>(null));
                break;
        }

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateAsync(ValidCreate(assigneeId: assigneeId), CancellationToken.None));

        Assert.Empty(_db.Incidents);
    }

    // ==================== GetById ====================

    [Fact]
    public async Task GetById_InMyTeam_ReturnsIncident_WithAllowedNextStatuses()
    {
        var incident = await SeedIncidentAsync();

        var result = await _sut.GetByIdAsync(incident.Id, CancellationToken.None);

        Assert.Equal(incident.Id, result.Id);
        Assert.Equal(
            new[] { IncidentStatus.Investigating, IncidentStatus.Resolved },
            result.AllowedNextStatuses);
    }

    [Fact]
    public async Task GetById_OfAnotherTeam_ThrowsNotFound()
    {
        var foreign = await SeedIncidentAsync(teamId: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByIdAsync(foreign.Id, CancellationToken.None));
    }

    // ==================== List ====================

    [Fact]
    public async Task List_ReturnsOnlyMyTeamsIncidents()
    {
        await SeedIncidentAsync(title: "Mine A");
        await SeedIncidentAsync(title: "Mine B");
        await SeedIncidentAsync(teamId: Guid.NewGuid(), title: "Not mine");

        var result = await _sut.ListAsync(new ListIncidentsQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.DoesNotContain(result.Items, i => i.Title == "Not mine");
    }

    [Fact]
    public async Task List_SortedBySeverityDescending_PutsCriticalFirstAndLowLast()
    {
        await SeedIncidentAsync(severity: IncidentSeverity.Low);
        await SeedIncidentAsync(severity: IncidentSeverity.Critical);
        await SeedIncidentAsync(severity: IncidentSeverity.Medium);
        await SeedIncidentAsync(severity: IncidentSeverity.High);

        var result = await _sut.ListAsync(
            new ListIncidentsQuery(SortBy: "severity", SortDirection: "desc"), CancellationToken.None);

        Assert.Equal(
            new[] { IncidentSeverity.Critical, IncidentSeverity.High, IncidentSeverity.Medium, IncidentSeverity.Low },
            result.Items.Select(i => i.Severity));
    }

    [Fact]
    public async Task List_FiltersBySeverityAndSearch()
    {
        await SeedIncidentAsync(severity: IncidentSeverity.High, title: "Payment gateway timeout");
        await SeedIncidentAsync(severity: IncidentSeverity.Low, title: "Payment page typo");
        await SeedIncidentAsync(severity: IncidentSeverity.High, title: "Login broken");

        var result = await _sut.ListAsync(
            new ListIncidentsQuery(Severity: IncidentSeverity.High, Search: "PAYMENT"), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Payment gateway timeout", result.Items[0].Title);
    }

    [Theory]
    [InlineData("password", "asc")]       // kolom di luar daftar izin
    [InlineData("createdAt", "sideways")] // arah tidak dikenal
    public async Task List_WithInvalidSorting_ThrowsValidation(string sortBy, string direction)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.ListAsync(new ListIncidentsQuery(SortBy: sortBy, SortDirection: direction), CancellationToken.None));
    }

    // ==================== Update ====================

    [Fact]
    public async Task Update_AsViewer_ThrowsForbidden()
    {
        var incident = await SeedIncidentAsync();
        _currentUser.Role.Returns(UserRole.Viewer);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.UpdateAsync(incident.Id, new UpdateIncidentRequest(Title: "Judul baru yang valid"), CancellationToken.None));
    }

    [Fact]
    public async Task Update_ChangingTheTitle_SavesIt_AndRecordsAnUpdatedActivity()
    {
        var incident = await SeedIncidentAsync();

        await _sut.UpdateAsync(incident.Id, new UpdateIncidentRequest(Title: "Judul baru yang valid"), CancellationToken.None);

        Assert.Equal("Judul baru yang valid", (await _db.Incidents.SingleAsync()).Title);
        Assert.Contains(await ActivitiesOfAsync(incident.Id), a => a.Type == ActivityType.Updated);
    }

    [Fact]
    public async Task Update_WithTheSameValues_RecordsNoActivity()
    {
        var incident = await SeedIncidentAsync(severity: IncidentSeverity.Medium, title: "Seeded incident");

        await _sut.UpdateAsync(incident.Id,
            new UpdateIncidentRequest(Title: "Seeded incident", Severity: IncidentSeverity.Medium), CancellationToken.None);

        Assert.Empty(await ActivitiesOfAsync(incident.Id));
    }

    [Fact]
    public async Task Update_Severity_RecalculatesDueDates_AndRecordsASeverityActivity()
    {
        var incident = await SeedIncidentAsync(severity: IncidentSeverity.Low);

        var result = await _sut.UpdateAsync(incident.Id,
            new UpdateIncidentRequest(Severity: IncidentSeverity.Critical), CancellationToken.None);

        Assert.Equal(_now.AddMinutes(15), result.ResponseDueAtUtc);
        Assert.Equal(_now.AddHours(4), result.ResolutionDueAtUtc);
        Assert.Contains(await ActivitiesOfAsync(incident.Id), a => a.Type == ActivityType.SeverityChanged);
    }

    [Fact]
    public async Task Update_OfAnotherTeam_ThrowsNotFound()
    {
        var foreign = await SeedIncidentAsync(teamId: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.UpdateAsync(foreign.Id, new UpdateIncidentRequest(Title: "Judul baru yang valid"), CancellationToken.None));
    }

    [Fact]
    public async Task Update_AClosedIncident_ThrowsDomainRule()
    {
        var incident = await SeedIncidentAsync();
        incident.Status = IncidentStatus.Closed;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _sut.UpdateAsync(incident.Id, new UpdateIncidentRequest(Title: "Judul baru yang valid"), CancellationToken.None));
    }

    // ==================== ChangeStatus ====================

    [Theory]
    [InlineData(IncidentStatus.Investigating, ActivityType.StatusChanged)]
    [InlineData(IncidentStatus.Resolved, ActivityType.Resolved)]
    public async Task ChangeStatus_FromOpen_UpdatesIncident_AndRecordsTheMatchingActivity(
        IncidentStatus target, ActivityType expectedActivity)
    {
        var incident = await SeedIncidentAsync();

        var result = await _sut.ChangeStatusAsync(incident.Id, new ChangeStatusRequest(target), CancellationToken.None);

        Assert.Equal(target, result.Status);

        var activities = await ActivitiesOfAsync(incident.Id);
        Assert.Single(activities);
        Assert.Equal(expectedActivity, activities[0].Type);
    }

    [Fact]
    public async Task ChangeStatus_WithIllegalTransition_ThrowsDomainRule_AndRecordsNothing()
    {
        var incident = await SeedIncidentAsync();   // Open

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            _sut.ChangeStatusAsync(incident.Id, new ChangeStatusRequest(IncidentStatus.Closed), CancellationToken.None));

        Assert.Equal(IncidentStatus.Open, (await _db.Incidents.SingleAsync()).Status);
        Assert.Empty(await ActivitiesOfAsync(incident.Id));
    }

    [Fact]
    public async Task ChangeStatus_WithoutStatus_ThrowsValidation()
    {
        var incident = await SeedIncidentAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.ChangeStatusAsync(incident.Id, new ChangeStatusRequest(null), CancellationToken.None));
    }

    // ==================== Assign ====================

    [Fact]
    public async Task Assign_AsViewer_ThrowsForbidden()
    {
        var incident = await SeedIncidentAsync();
        _currentUser.Role.Returns(UserRole.Viewer);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.AssignAsync(incident.Id, new AssignIncidentRequest(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Assign_ToAValidUser_SetsTheAssignee_AndRecordsAnActivity()
    {
        var incident = await SeedIncidentAsync();
        var assigneeId = Guid.NewGuid();
        GivenAssignee(assigneeId);

        var result = await _sut.AssignAsync(incident.Id, new AssignIncidentRequest(assigneeId), CancellationToken.None);

        Assert.Equal(assigneeId, result.AssigneeId);
        Assert.Single(await ActivitiesOfAsync(incident.Id));
    }

    [Fact]
    public async Task Assign_TheSameAssigneeAgain_RecordsNoExtraActivity()
    {
        var incident = await SeedIncidentAsync();
        var assigneeId = Guid.NewGuid();
        GivenAssignee(assigneeId);

        await _sut.AssignAsync(incident.Id, new AssignIncidentRequest(assigneeId), CancellationToken.None);
        await _sut.AssignAsync(incident.Id, new AssignIncidentRequest(assigneeId), CancellationToken.None);

        Assert.Single(await ActivitiesOfAsync(incident.Id));
    }

    [Fact]
    public async Task Assign_Null_RemovesTheAssignee()
    {
        var incident = await SeedIncidentAsync();
        var assigneeId = Guid.NewGuid();
        GivenAssignee(assigneeId);
        await _sut.AssignAsync(incident.Id, new AssignIncidentRequest(assigneeId), CancellationToken.None);

        var result = await _sut.AssignAsync(incident.Id, new AssignIncidentRequest(null), CancellationToken.None);

        Assert.Null(result.AssigneeId);
        Assert.Equal(2, (await ActivitiesOfAsync(incident.Id)).Count);
    }

    [Fact]
    public async Task Assign_ToAnInactiveUser_ThrowsValidation_AndChangesNothing()
    {
        var incident = await SeedIncidentAsync();
        var assigneeId = Guid.NewGuid();
        GivenAssignee(assigneeId, isActive: false);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.AssignAsync(incident.Id, new AssignIncidentRequest(assigneeId), CancellationToken.None));

        Assert.Null((await _db.Incidents.SingleAsync()).AssigneeId);
    }
}