using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OpsPilot.Application.Comments;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Tests.Incidents;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Application.Tests.Comments;

public class CommentServiceTests
{
    private readonly TestDbContext _db = TestDbContext.CreateNew();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly DateTimeOffset _now = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);
    private readonly Guid _myTeamId = Guid.NewGuid();
    private readonly Guid _myUserId = Guid.NewGuid();
    private readonly CommentService _sut;

    public CommentServiceTests()
    {
        _currentUser.TeamId.Returns(_myTeamId);
        _currentUser.UserId.Returns(_myUserId);
        _currentUser.Role.Returns(UserRole.Responder);   // bawaan: pemanggil adalah Responder

        // Pencarian nama: user saya bernama "Demo Responder"
        IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string> { [_myUserId] = "Demo Responder" };
        _identity
            .GetDisplayNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(names);

        _sut = new CommentService(
            new PageQueryValidator(),
            new CreateCommentRequestValidator(),
            _db,
            _identity,
            _currentUser,
            new FixedTimeProvider(_now));
    }

    // ---------- Helper ----------

    private async Task<Incident> SeedIncidentAsync(Guid? teamId = null)
    {
        var incident = Incident.Create(
            teamId ?? _myTeamId, _myUserId, "Seeded incident", "Seeded description",
            IncidentSeverity.Medium, null, SlaPolicy.Default(), _now);

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
        return incident;
    }

    // ==================== AddAsync ====================

    [Fact]
    public async Task Add_AsViewer_ThrowsForbidden_AndSavesNothing()
    {
        var incident = await SeedIncidentAsync();
        _currentUser.Role.Returns(UserRole.Viewer);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.AddAsync(incident.Id, new CreateCommentRequest("Halo"), CancellationToken.None));

        Assert.Empty(_db.IncidentComments);
        Assert.Empty(_db.IncidentActivities);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Add_WithEmptyBody_ThrowsValidation_AndSavesNothing(string body)
    {
        var incident = await SeedIncidentAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.AddAsync(incident.Id, new CreateCommentRequest(body), CancellationToken.None));

        Assert.Empty(_db.IncidentComments);
    }

    [Fact]
    public async Task Add_OnAnotherTeamsIncident_ThrowsNotFound_AndSavesNothing()
    {
        var foreign = await SeedIncidentAsync(teamId: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.AddAsync(foreign.Id, new CreateCommentRequest("Halo"), CancellationToken.None));

        Assert.Empty(_db.IncidentComments);
        Assert.Empty(_db.IncidentActivities);
    }

    [Fact]
    public async Task Add_Valid_SavesTheTrimmedComment_AndExactlyOneCommentAddedActivity()
    {
        var incident = await SeedIncidentAsync();

        var result = await _sut.AddAsync(
            incident.Id, new CreateCommentRequest("  Penyebabnya disk penuh.  "), CancellationToken.None);

        var comment = await _db.IncidentComments.SingleAsync();
        Assert.Equal("Penyebabnya disk penuh.", comment.Body);   // spasi di ujung dibuang
        Assert.Equal(_myUserId, comment.AuthorId);               // penulis dari token
        Assert.Equal(_now, comment.CreatedAtUtc);
        Assert.Equal("Demo Responder", result.AuthorName);

        var activity = await _db.IncidentActivities.SingleAsync();
        Assert.Equal(ActivityType.CommentAdded, activity.Type);
        Assert.Equal(_myUserId, activity.ActorId);
        Assert.Equal(incident.Id, activity.IncidentId);
        Assert.Contains(comment.Id.ToString(), activity.MetadataJson);   // activity menunjuk ke komentarnya
    }

    [Fact]
    public async Task Add_OnAClosedIncident_IsAllowed()
    {
        var incident = await SeedIncidentAsync();
        incident.Status = IncidentStatus.Closed;
        await _db.SaveChangesAsync();

        var result = await _sut.AddAsync(incident.Id, new CreateCommentRequest("Tindak lanjut"), CancellationToken.None);

        Assert.Equal("Tindak lanjut", result.Body);
    }

    [Fact]
    public async Task Add_DoesNotChangeTheIncidentItself()
    {
        var incident = await SeedIncidentAsync();
        var updatedBefore = incident.UpdatedAtUtc;

        await _sut.AddAsync(incident.Id, new CreateCommentRequest("Halo"), CancellationToken.None);

        var after = await _db.Incidents.AsNoTracking().SingleAsync();
        Assert.Equal(updatedBefore, after.UpdatedAtUtc);
        Assert.Equal(IncidentStatus.Open, after.Status);
    }

    // ==================== ListAsync ====================

    [Fact]
    public async Task List_ReturnsOnlyThisIncidentsComments_OldestFirst_WithAuthorNames()
    {
        var incident = await SeedIncidentAsync();
        var other = await SeedIncidentAsync();

        // Sengaja dimasukkan TIDAK berurutan, untuk membuktikan hasilnya diurutkan
        _db.IncidentComments.Add(IncidentComment.Create(incident.Id, _myUserId, "Kedua", _now.AddMinutes(2)));
        _db.IncidentComments.Add(IncidentComment.Create(incident.Id, _myUserId, "Pertama", _now.AddMinutes(1)));
        _db.IncidentComments.Add(IncidentComment.Create(other.Id, _myUserId, "Milik incident lain", _now));
        await _db.SaveChangesAsync();

        var result = await _sut.ListAsync(incident.Id, new PageQuery(), CancellationToken.None);

        Assert.Equal(new[] { "Pertama", "Kedua" }, result.Items.Select(c => c.Body));
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, c => Assert.Equal("Demo Responder", c.AuthorName));
    }

    [Fact]
    public async Task List_Paging_SplitsIntoPages()
    {
        var incident = await SeedIncidentAsync();
        for (var i = 1; i <= 3; i++)
            _db.IncidentComments.Add(IncidentComment.Create(incident.Id, _myUserId, $"Komentar {i}", _now.AddMinutes(i)));
        await _db.SaveChangesAsync();

        var page2 = await _sut.ListAsync(incident.Id, new PageQuery(Page: 2, PageSize: 2), CancellationToken.None);

        Assert.Single(page2.Items);
        Assert.Equal("Komentar 3", page2.Items[0].Body);
        Assert.Equal(3, page2.TotalCount);
        Assert.Equal(2, page2.TotalPges);
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
            _sut.ListAsync(incident.Id, new PageQuery(PageSize: 1000), CancellationToken.None));
    }
}