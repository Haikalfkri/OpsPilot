using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class TeamsEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Current_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/teams/current");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(TestUsers.Admin, "Platform Team")]
    [InlineData(TestUsers.Viewer, "Platform Team")]
    [InlineData(TestUsers.OtherTeamResponder, "Other Team")]
    public async Task Current_ReturnsTheCallersOwnTeam(string email, string expectedTeamName)
    {
        var client = await factory.CreateAuthenticatedClientAsync(email);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/teams/current");

        Assert.Equal(expectedTeamName, body.GetProperty("name").GetString());
    }
}