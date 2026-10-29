using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class HealthEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_WithoutToken_ReturnsHealthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
    }
}