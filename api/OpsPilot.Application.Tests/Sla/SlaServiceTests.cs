using OpsPilot.Application.Sla;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Application.Tests.Sla;

public class SlaServiceTests
{
    private readonly SlaService _sut = new(SlaPolicy.Default());

    [Fact]
    public void GetPolicy_ReturnsAllFourSeverities_HighestFirst_InMinutes()
    {
        var policy = _sut.GetPolicy();

        var expected = new[]
        {
            IncidentSeverity.Critical, IncidentSeverity.High, IncidentSeverity.Medium, IncidentSeverity.Low
        };

        var actual = policy.Targets.Select(t => t.Severity).ToArray();

        Assert.Equal<IncidentSeverity>(expected, actual);

        var critical = policy.Targets[0];
        Assert.Equal(15, critical.ResponseMinutes);
        Assert.Equal(240, critical.ResolutionMinutes);

        var low = policy.Targets[3];
        Assert.Equal(480, low.ResponseMinutes);
        Assert.Equal(4320, low.ResolutionMinutes);
    }

    [Fact]
    public void GetPolicy_SaysTheValuesAreExamples()
    {
        var policy = _sut.GetPolicy();

        Assert.Contains("demo", policy.Note, StringComparison.OrdinalIgnoreCase);
    }
}