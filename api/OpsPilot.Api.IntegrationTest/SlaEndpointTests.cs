using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class SlaEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Policy_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/sla/policy");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(TestUsers.Admin)]
    [InlineData(TestUsers.Responder)]
    [InlineData(TestUsers.Viewer)]
    public async Task Policy_IsReadableByEveryRole(string email)
    {
        var client = await factory.CreateAuthenticatedClientAsync(email);

        var response = await client.GetAsync("/api/v1/sla/policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Policy_ReturnsTheConfiguredTargets_HighestSeverityFirst_AndAnExampleNote()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Viewer);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/sla/policy");

        var targets = body.GetProperty("targets").EnumerateArray().ToList();
        Assert.Equal(4, targets.Count);
        Assert.Equal(
            new[] { "Critical", "High", "Medium", "Low" },
            targets.Select(t => t.GetProperty("severity").GetString()));

        // Nilai dari appsettings.json: Critical respons 15 menit, penyelesaian 4 jam
        Assert.Equal(15, targets[0].GetProperty("responseMinutes").GetInt32());
        Assert.Equal(240, targets[0].GetProperty("resolutionMinutes").GetInt32());

        Assert.Contains("demo", body.GetProperty("note").GetString(), StringComparison.OrdinalIgnoreCase);
    }
}