using Microsoft.Extensions.Configuration;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Sla;
using Xunit;

namespace OpsPilot.Infrastructure.Tests;

public class SlaPolicyFactoryTests
{
    // Membuat konfigurasi palsu di memori dari pasangan "kunci: nilai"
    private static IConfigurationSection Section(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("Sla");

    private static Dictionary<string, string?> FullConfig() => new()
    {
        ["Sla:Critical:ResponseMinutes"] = "5",
        ["Sla:Critical:ResolutionMinutes"] = "60",
        ["Sla:High:ResponseMinutes"] = "30",
        ["Sla:High:ResolutionMinutes"] = "120",
        ["Sla:Medium:ResponseMinutes"] = "60",
        ["Sla:Medium:ResolutionMinutes"] = "240",
        ["Sla:Low:ResponseMinutes"] = "120",
        ["Sla:Low:ResolutionMinutes"] = "480"
    };

    [Fact]
    public void Create_WithoutAnySlaSection_UsesTheDefaultExamples()
    {
        var policy = SlaPolicyFactory.Create(Section(new Dictionary<string, string?>()));

        Assert.Equal(TimeSpan.FromMinutes(15), policy.GetTargets(IncidentSeverity.Critical).ResponseTarget);
        Assert.Equal(TimeSpan.FromHours(72), policy.GetTargets(IncidentSeverity.Low).ResolutionTarget);
    }

    [Fact]
    public void Create_WithAFullSection_ReadsTheConfiguredMinutes()
    {
        var policy = SlaPolicyFactory.Create(Section(FullConfig()));

        Assert.Equal(TimeSpan.FromMinutes(5), policy.GetTargets(IncidentSeverity.Critical).ResponseTarget);
        Assert.Equal(TimeSpan.FromMinutes(60), policy.GetTargets(IncidentSeverity.Critical).ResolutionTarget);
        Assert.Equal(TimeSpan.FromMinutes(480), policy.GetTargets(IncidentSeverity.Low).ResolutionTarget);
    }

    [Fact]
    public void Create_WhenASeverityIsMissing_Throws()
    {
        var config = FullConfig();
        config.Remove("Sla:High:ResponseMinutes");
        config.Remove("Sla:High:ResolutionMinutes");

        var error = Assert.Throws<InvalidOperationException>(() => SlaPolicyFactory.Create(Section(config)));

        Assert.Contains("High", error.Message);   // pesannya menyebut severity yang kurang
    }

    [Fact]
    public void Create_WhenOneNumberIsMissing_Throws()
    {
        var config = FullConfig();
        config.Remove("Sla:Medium:ResolutionMinutes");

        Assert.Throws<InvalidOperationException>(() => SlaPolicyFactory.Create(Section(config)));
    }

    [Fact]
    public void Create_WhenAValueIsZero_Throws()
    {
        var config = FullConfig();
        config["Sla:Critical:ResponseMinutes"] = "0";

        Assert.Throws<ArgumentException>(() => SlaPolicyFactory.Create(Section(config)));
    }

    [Fact]
    public void Create_WhenResolutionIsShorterThanResponse_Throws()
    {
        var config = FullConfig();
        config["Sla:Critical:ResponseMinutes"] = "90";
        config["Sla:Critical:ResolutionMinutes"] = "60";

        Assert.Throws<ArgumentException>(() => SlaPolicyFactory.Create(Section(config)));
    }
}