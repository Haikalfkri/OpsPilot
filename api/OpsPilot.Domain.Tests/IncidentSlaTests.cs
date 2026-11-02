using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Domain.Tests;

/// <summary>
/// Critical: respons dalam 15 menit, penyelesaian dalam 4 jam (dari waktu dibuat).
/// </summary>
public class IncidentSlaTests
{
    private static readonly SlaPolicy Sla = SlaPolicy.Default();
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private static Incident NewIncident(IncidentSeverity severity = IncidentSeverity.Critical) =>
        Incident.Create(Guid.NewGuid(), Guid.NewGuid(), "Database down", "Cannot connect", severity, null, Sla, T0);

    [Fact]
    public void JustCreated_BothTargetsArePending()
    {
        var incident = NewIncident();
        var now = T0.AddMinutes(1);

        Assert.Equal(SlaState.Pending, incident.GetResponseSla(now));
        Assert.Equal(SlaState.Pending, incident.GetResolutionSla(now));
    }

    [Fact]
    public void NobodyRespondedAfterTheResponseTarget_ResponseIsBreached_ResolutionStillPending()
    {
        var incident = NewIncident();
        var now = T0.AddMinutes(16);          // batas respons: menit ke-15

        Assert.Equal(SlaState.Breached, incident.GetResponseSla(now));
        Assert.Equal(SlaState.Pending, incident.GetResolutionSla(now));
    }

    [Fact]
    public void NobodyDidAnythingAfterTheResolutionTarget_BothAreBreached()
    {
        var incident = NewIncident();
        var now = T0.AddHours(4).AddMinutes(1);

        Assert.Equal(SlaState.Breached, incident.GetResponseSla(now));
        Assert.Equal(SlaState.Breached, incident.GetResolutionSla(now));
    }

    [Fact]
    public void RespondedInTime_ResponseStaysMet_EvenLongAfterwards()
    {
        var incident = NewIncident();
        incident.ChangeStatus(IncidentStatus.Investigating, T0.AddMinutes(10));

        Assert.Equal(SlaState.Met, incident.GetResponseSla(T0.AddHours(3)));
        Assert.Equal(SlaState.Pending, incident.GetResolutionSla(T0.AddHours(3)));
    }

    [Fact]
    public void RespondedAfterTheTarget_ResponseIsBreached_EvenThoughSomeoneRespondedEventually()
    {
        var incident = NewIncident();
        incident.ChangeStatus(IncidentStatus.Investigating, T0.AddMinutes(20));   // batas: menit ke-15

        Assert.Equal(SlaState.Breached, incident.GetResponseSla(T0.AddMinutes(21)));
    }

    [Fact]
    public void ResolvedInTime_ResolutionIsMet()
    {
        var incident = NewIncident();
        incident.ChangeStatus(IncidentStatus.Investigating, T0.AddMinutes(5));
        incident.ChangeStatus(IncidentStatus.Resolved, T0.AddHours(3));

        Assert.Equal(SlaState.Met, incident.GetResolutionSla(T0.AddHours(10)));
    }

    [Fact]
    public void ResolvedAfterTheTarget_ResolutionIsBreached()
    {
        var incident = NewIncident();
        incident.ChangeStatus(IncidentStatus.Investigating, T0.AddMinutes(5));
        incident.ChangeStatus(IncidentStatus.Resolved, T0.AddHours(5));           // batas: 4 jam

        Assert.Equal(SlaState.Breached, incident.GetResolutionSla(T0.AddHours(10)));
    }

    [Fact]
    public void ResolvedDirectlyFromOpen_CountsAsTheResponseToo()
    {
        var incident = NewIncident();
        incident.ChangeStatus(IncidentStatus.Resolved, T0.AddMinutes(10));        // tanpa Investigating

        var now = T0.AddHours(1);
        Assert.Equal(SlaState.Met, incident.GetResponseSla(now));
        Assert.Equal(SlaState.Met, incident.GetResolutionSla(now));
        Assert.Null(incident.FirstRespondedAtUtc);                                // tetap tidak tercatat sebagai respons
    }

    [Fact]
    public void ResolvedDirectlyFromOpen_AfterTheResponseTarget_ResponseIsBreached_ButResolutionIsMet()
    {
        var incident = NewIncident();
        incident.ChangeStatus(IncidentStatus.Resolved, T0.AddMinutes(20));

        var now = T0.AddHours(1);
        Assert.Equal(SlaState.Breached, incident.GetResponseSla(now));
        Assert.Equal(SlaState.Met, incident.GetResolutionSla(now));
    }

    [Fact]
    public void ChangingSeverityToCritical_ReassessesWithTheNewTargets()
    {
        // Low: batas respons 8 jam. Satu jam setelah dibuat masih aman.
        var incident = NewIncident(IncidentSeverity.Low);
        var oneHourLater = T0.AddHours(1);
        Assert.Equal(SlaState.Pending, incident.GetResponseSla(oneHourLater));

        // Dinaikkan ke Critical: batas respons jadi 15 menit dari waktu dibuat, jadi sudah lewat
        incident.ChangeSeverity(IncidentSeverity.Critical, Sla, oneHourLater);

        Assert.Equal(SlaState.Breached, incident.GetResponseSla(oneHourLater));
    }
}