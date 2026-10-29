using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class TeamMembersEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task ListMembers_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/teams/current/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListMembers_AsViewer_ReturnsOnlyActiveUsersOfOwnTeam_WithoutEmails()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Viewer);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/teams/current/users?pageSize=100");

        var items = body.GetProperty("items").EnumerateArray().ToList();
        var names = items.Select(i => i.GetProperty("displayName").GetString()).ToList();

        Assert.Contains("Demo Admin", names);
        Assert.Contains("Demo Responder", names);
        Assert.DoesNotContain("Demo Inactive", names);          // user nonaktif tidak tampil
        Assert.DoesNotContain("Other Team Responder", names);   // tim lain tidak bocor
        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());

        // data ringkas: tidak ada email
        Assert.All(items, item => Assert.False(item.TryGetProperty("email", out _)));
    }

    [Fact]
    public async Task ListMembers_CannotBeTrickedIntoShowingInactiveUsers()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/teams/current/users?isActive=false");

        // Filter isActive dari client diabaikan: tetap user aktif saja
        var names = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("displayName").GetString());
        Assert.DoesNotContain("Demo Inactive", names);
    }

    [Fact]
    public async Task ListMembers_WithPageSizeTooLarge_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Viewer);

        var response = await client.GetAsync("/api/v1/teams/current/users?pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}