using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class UsersEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(TestUsers.Responder)]
    [InlineData(TestUsers.Viewer)]
    public async Task List_AsNonAdmin_Returns403(string email)
    {
        var client = await factory.CreateAuthenticatedClientAsync(email);

        var response = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_AsAdmin_ContainsOnlyUsersOfTheSameTeam()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/users?pageSize=100");

        var emails = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("email").GetString())
            .ToList();

        Assert.Contains(TestUsers.Admin, emails);
        Assert.Contains(TestUsers.Inactive, emails);
        Assert.DoesNotContain(TestUsers.OtherTeamResponder, emails);   // user tim lain tidak boleh bocor
        Assert.Equal(4, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task List_FilteredByRole_ReturnsOnlyThatRole()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/users?role=Responder");

        var roles = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("role").GetString())
            .ToList();

        Assert.Equal(2, roles.Count);                 // responder + inactive
        Assert.All(roles, role => Assert.Equal("Responder", role));
    }

    [Fact]
    public async Task List_SearchByName_ReturnsMatchingUser()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/users?search=viewer");

        var items = body.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(TestUsers.Viewer, items[0].GetProperty("email").GetString());
    }

    [Fact]
    public async Task List_WithPageSizeTooLarge_Returns400WithFieldError()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);

        var response = await client.GetAsync("/api/v1/users?pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("pageSize", out _));
    }
}