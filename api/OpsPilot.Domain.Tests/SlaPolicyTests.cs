using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Domain.Tests;

public class SlaPolicyTests
{
    [Theory]
    [InlineData(IncidentSeverity.Critical, 15, 240)]     // 15 menit, 4 jam
    [InlineData(IncidentSeverity.High, 60, 480)]         // 1 jam, 8 jam
    [InlineData(IncidentSeverity.Medium, 240, 1440)]     // 4 jam, 24 jam
    [InlineData(IncidentSeverity.Low, 480, 4320)]        // 8 jam, 72 jam
    public void Default_HasTheDocumentedTargets(IncidentSeverity severity, int responseMinutes, int resolutionMinutes)
    {
        var targets = SlaPolicy.Default().GetTargets(severity);

        Assert.Equal(TimeSpan.FromMinutes(responseMinutes), targets.ResponseTarget);
        Assert.Equal(TimeSpan.FromMinutes(resolutionMinutes), targets.ResolutionTarget);
    }

    [Fact]
    public void CalculateDueDates_AddsTheTargetsToTheCreationTime()
    {
        var created = new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

        var (responseDue, resolutionDue) = SlaPolicy.Default().CalculateDueDates(created, IncidentSeverity.Critical);

        Assert.Equal(created.AddMinutes(15), responseDue);
        Assert.Equal(created.AddHours(4), resolutionDue);
    }

    [Fact]
    public void Constructor_WithMissingSeverity_Throws()
    {
        var incomplete = new Dictionary<IncidentSeverity, SlaTargets>
        {
            [IncidentSeverity.Low] = new(TimeSpan.FromHours(1), TimeSpan.FromHours(2))
        };

        Assert.Throws<ArgumentException>(() => new SlaPolicy(incomplete));
    }
}