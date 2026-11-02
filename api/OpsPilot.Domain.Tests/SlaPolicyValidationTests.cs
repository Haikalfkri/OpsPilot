using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Domain.Tests;

public class SlaPolicyValidationTests
{
    // Membuat kebijakan lengkap, dengan satu severity (Critical) yang bisa diganti per tes
    private static Dictionary<IncidentSeverity, SlaTargets> AllSeverities(SlaTargets critical) => new()
    {
        [IncidentSeverity.Critical] = critical,
        [IncidentSeverity.High] = new(TimeSpan.FromHours(1), TimeSpan.FromHours(8)),
        [IncidentSeverity.Medium] = new(TimeSpan.FromHours(4), TimeSpan.FromHours(24)),
        [IncidentSeverity.Low] = new(TimeSpan.FromHours(8), TimeSpan.FromHours(72))
    };

    [Fact]
    public void Constructor_WithValidTargets_Works()
    {
        var policy = new SlaPolicy(AllSeverities(new(TimeSpan.FromMinutes(5), TimeSpan.FromHours(1))));

        Assert.Equal(TimeSpan.FromMinutes(5), policy.GetTargets(IncidentSeverity.Critical).ResponseTarget);
    }

    [Theory]
    [InlineData(0, 60)]      // respons 0 menit
    [InlineData(-5, 60)]     // respons negatif
    [InlineData(15, 0)]      // penyelesaian 0 menit
    [InlineData(15, -1)]     // penyelesaian negatif
    public void Constructor_WithZeroOrNegativeTargets_Throws(int responseMinutes, int resolutionMinutes)
    {
        var targets = AllSeverities(new(TimeSpan.FromMinutes(responseMinutes), TimeSpan.FromMinutes(resolutionMinutes)));

        Assert.Throws<ArgumentException>(() => new SlaPolicy(targets));
    }

    [Fact]
    public void Constructor_WhenResolutionIsShorterThanResponse_Throws()
    {
        // Penyelesaian (10 menit) tidak mungkin lebih cepat dari respons (30 menit)
        var targets = AllSeverities(new(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(10)));

        Assert.Throws<ArgumentException>(() => new SlaPolicy(targets));
    }

    [Fact]
    public void Constructor_WhenResolutionEqualsResponse_IsAllowed()
    {
        var targets = AllSeverities(new(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30)));

        var policy = new SlaPolicy(targets);

        Assert.Equal(TimeSpan.FromMinutes(30), policy.GetTargets(IncidentSeverity.Critical).ResolutionTarget);
    }
}