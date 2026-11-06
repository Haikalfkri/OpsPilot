using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Domain.Tests;

public class PostmortemTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private static Postmortem NewPostmortem() =>
        Postmortem.Create(Guid.NewGuid(), Guid.NewGuid(), "Summary", "Root cause", "Impact", "Actions", T0);

    [Fact]
    public void Create_StoresEverything_AndStartsWithTheCreatedTimeEqualToTheUpdatedTime()
    {
        var incidentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var postmortem = Postmortem.Create(incidentId, authorId, "S", "R", "I", "C", T0);

        Assert.NotEqual(Guid.Empty, postmortem.Id);
        Assert.Equal(incidentId, postmortem.IncidentId);
        Assert.Equal(authorId, postmortem.AuthorId);
        Assert.Equal("S", postmortem.Summary);
        Assert.Equal("R", postmortem.RootCause);
        Assert.Equal("I", postmortem.Impact);
        Assert.Equal("C", postmortem.CorrectiveActions);
        Assert.Equal(T0, postmortem.CreatedAtUtc);
        Assert.Equal(T0, postmortem.UpdatedAtUtc);
    }

    [Fact]
    public void Update_WithNewContent_ReturnsTrue_ChangesTheFields_AndTouchesOnlyTheUpdatedTime()
    {
        var postmortem = NewPostmortem();
        var authorId = postmortem.AuthorId;

        var changed = postmortem.Update("New summary", "Root cause", "Impact", "Actions", T0.AddHours(1));

        Assert.True(changed);
        Assert.Equal("New summary", postmortem.Summary);
        Assert.Equal(T0.AddHours(1), postmortem.UpdatedAtUtc);

        // Yang tidak boleh berubah
        Assert.Equal(T0, postmortem.CreatedAtUtc);
        Assert.Equal(authorId, postmortem.AuthorId);
    }

    [Theory]
    [InlineData("Summary", "Root cause", "Impact", "Changed")]
    [InlineData("Summary", "Root cause", "Changed", "Actions")]
    [InlineData("Summary", "Changed", "Impact", "Actions")]
    [InlineData("Changed", "Root cause", "Impact", "Actions")]
    public void Update_NoticesAChangeInAnySingleField(string summary, string rootCause, string impact, string actions)
    {
        var postmortem = NewPostmortem();

        Assert.True(postmortem.Update(summary, rootCause, impact, actions, T0.AddHours(1)));
    }

    [Fact]
    public void Update_WithIdenticalContent_ReturnsFalse_AndChangesNothing()
    {
        var postmortem = NewPostmortem();

        var changed = postmortem.Update("Summary", "Root cause", "Impact", "Actions", T0.AddHours(1));

        Assert.False(changed);
        Assert.Equal(T0, postmortem.UpdatedAtUtc);     // waktu diubah tidak ikut berubah
    }
}

public class IncidentPostmortemRuleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private static Incident IncidentWithStatus(IncidentStatus status)
    {
        var incident = Incident.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Database down", "Cannot connect",
            IncidentSeverity.High, null, SlaPolicy.Default(), T0);

        incident.Status = status;
        return incident;
    }

    [Theory]
    [InlineData(IncidentStatus.Open)]
    [InlineData(IncidentStatus.Investigating)]
    public void EnsureCanHavePostmortem_BeforeTheIncidentIsResolved_Throws(IncidentStatus status)
    {
        var incident = IncidentWithStatus(status);

        Assert.Throws<DomainRuleException>(() => incident.EnsureCanHavePostmortem());
    }

    [Theory]
    [InlineData(IncidentStatus.Resolved)]
    [InlineData(IncidentStatus.Closed)]
    public void EnsureCanHavePostmortem_ForResolvedOrClosed_DoesNotThrow(IncidentStatus status)
    {
        var incident = IncidentWithStatus(status);

        Assert.Null(Record.Exception(() => incident.EnsureCanHavePostmortem()));
    }

    [Fact]
    public void EnsureCanHavePostmortem_CoversEveryStatus_SoANewStatusCannotSlipThroughUnnoticed()
    {
        // Kalau suatu hari ada status baru, tes ini memaksa kita memutuskan apakah ia boleh punya postmortem
        var allowed = Enum.GetValues<IncidentStatus>()
            .Where(status => Record.Exception(() => IncidentWithStatus(status).EnsureCanHavePostmortem()) is null)
            .ToArray();

        Assert.Equal(new[] { IncidentStatus.Resolved, IncidentStatus.Closed }, allowed);
    }
}