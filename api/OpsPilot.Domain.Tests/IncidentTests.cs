using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Domain.Tests;

public class IncidentTests
{
    private static readonly SlaPolicy Sla = SlaPolicy.Default();
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private static Incident NewIncident(IncidentSeverity severity = IncidentSeverity.High) =>
        Incident.Create(Guid.NewGuid(), Guid.NewGuid(), "Database down", "Cannot connect", severity, null, Sla, T0);

    // ---------- Create ----------

    [Fact]
    public void Create_StartsOpen_WithSlaTargetsFromTheSeverity()
    {
        var incident = NewIncident(IncidentSeverity.Critical);

        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Equal(T0, incident.CreatedAtUtc);
        Assert.Equal(T0.AddMinutes(15), incident.ResponseDueAtUtc);
        Assert.Equal(T0.AddHours(4), incident.ResolutionDueAtUtc);
        Assert.Null(incident.FirstRespondedAtUtc);
        Assert.Null(incident.ResolvedAtUtc);
    }

    // ---------- Status ----------

    [Fact]
    public void ChangeStatus_ToInvestigating_SetsFirstRespondedAt()
    {
        var incident = NewIncident();
        var t1 = T0.AddMinutes(5);

        incident.ChangeStatus(IncidentStatus.Investigating, t1);

        Assert.Equal(IncidentStatus.Investigating, incident.Status);
        Assert.Equal(t1, incident.FirstRespondedAtUtc);
        Assert.Equal(t1, incident.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeStatus_OpenDirectlyToResolved_SetsResolvedAt_ButNotFirstRespondedAt()
    {
        var incident = NewIncident();

        incident.ChangeStatus(IncidentStatus.Resolved, T0.AddMinutes(10));

        Assert.Equal(T0.AddMinutes(10), incident.ResolvedAtUtc);
        Assert.Null(incident.FirstRespondedAtUtc);
    }

    [Fact]
    public void ChangeStatus_FullLifecycle_SetsEachTimestamp()
    {
        var incident = NewIncident();

        incident.ChangeStatus(IncidentStatus.Investigating, T0.AddMinutes(1));
        incident.ChangeStatus(IncidentStatus.Resolved, T0.AddMinutes(2));
        incident.ChangeStatus(IncidentStatus.Closed, T0.AddMinutes(3));

        Assert.Equal(T0.AddMinutes(1), incident.FirstRespondedAtUtc);
        Assert.Equal(T0.AddMinutes(2), incident.ResolvedAtUtc);
        Assert.Equal(T0.AddMinutes(3), incident.ClosedAtUtc);
        Assert.Equal(IncidentStatus.Closed, incident.Status);
    }

    [Theory]
    [InlineData(IncidentStatus.Open, IncidentStatus.Open)]
    [InlineData(IncidentStatus.Open, IncidentStatus.Closed)]
    [InlineData(IncidentStatus.Investigating, IncidentStatus.Open)]
    [InlineData(IncidentStatus.Resolved, IncidentStatus.Open)]
    [InlineData(IncidentStatus.Resolved, IncidentStatus.Investigating)]
    [InlineData(IncidentStatus.Closed, IncidentStatus.Open)]
    [InlineData(IncidentStatus.Closed, IncidentStatus.Resolved)]
    public void ChangeStatus_WithIllegalTransition_Throws_AndChangesNothing(IncidentStatus from, IncidentStatus to)
    {
        var incident = NewIncident();
        incident.Status = from;
        var updatedBefore = incident.UpdatedAtUtc;

        Assert.Throws<DomainRuleException>(() => incident.ChangeStatus(to, T0.AddHours(1)));

        Assert.Equal(from, incident.Status);
        Assert.Equal(updatedBefore, incident.UpdatedAtUtc);
    }

    // ---------- Severity ----------

    [Fact]
    public void ChangeSeverity_RecalculatesDueDates_FromTheCreationTime_NotFromNow()
    {
        var incident = NewIncident(IncidentSeverity.High);
        var oneHourLater = T0.AddHours(1);

        incident.ChangeSeverity(IncidentSeverity.Critical, Sla, oneHourLater);

        // 15 menit dari T0 (waktu dibuat), BUKAN dari oneHourLater
        Assert.Equal(T0.AddMinutes(15), incident.ResponseDueAtUtc);
        Assert.Equal(T0.AddHours(4), incident.ResolutionDueAtUtc);
        Assert.Equal(oneHourLater, incident.UpdatedAtUtc);
    }

    // ---------- Closed = tidak boleh diubah ----------

    [Fact]
    public void ClosedIncident_RejectsEveryModification()
    {
        var incident = NewIncident();
        incident.Status = IncidentStatus.Closed;

        Assert.Throws<DomainRuleException>(() => incident.UpdateDetails("Judul baru", "Deskripsi", T0));
        Assert.Throws<DomainRuleException>(() => incident.ChangeSeverity(IncidentSeverity.Low, Sla, T0));
        Assert.Throws<DomainRuleException>(() => incident.AssignTo(Guid.NewGuid(), T0));
    }

    // ---------- Assign / Update ----------

    [Fact]
    public void AssignTo_SetsAndClearsTheAssignee()
    {
        var incident = NewIncident();
        var userId = Guid.NewGuid();

        incident.AssignTo(userId, T0.AddMinutes(1));
        Assert.Equal(userId, incident.AssigneeId);

        incident.AssignTo(null, T0.AddMinutes(2));
        Assert.Null(incident.AssigneeId);
    }

    [Fact]
    public void UpdateDetails_ChangesTitleAndDescription_AndTouchesUpdatedAt()
    {
        var incident = NewIncident();

        incident.UpdateDetails("Judul baru", "Deskripsi baru", T0.AddMinutes(7));

        Assert.Equal("Judul baru", incident.Title);
        Assert.Equal("Deskripsi baru", incident.Description);
        Assert.Equal(T0.AddMinutes(7), incident.UpdatedAtUtc);
    }
}