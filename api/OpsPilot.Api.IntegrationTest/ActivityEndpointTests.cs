using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class ActivityEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync($"/api/v1/incidents/{Guid.NewGuid()}/activities");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CanReadTheTimeline()
    {
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var viewer = await factory.CreateAuthenticatedClientAsync(TestUsers.Viewer);
        var incidentId = await TestIncidents.CreateAsync(responder);

        var response = await viewer.GetAsync($"/api/v1/incidents/{incidentId}/activities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Timeline_FollowsTheWholeLifecycle_InChronologicalOrder()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        // Urutan kejadian yang sengaja dibuat berurutan
        var incidentId = await TestIncidents.CreateAsync(client);                              // 1. Created
        await TestIncidents.ChangeStatusAsync(client, incidentId, "Investigating");            // 2. StatusChanged
        await TestIncidents.AddCommentAsync(client, incidentId, "Mulai investigasi");          // 3. CommentAdded
        await TestIncidents.ChangeStatusAsync(client, incidentId, "Resolved");                 // 4. Resolved
        await TestIncidents.ChangeStatusAsync(client, incidentId, "Closed");                   // 5. Closed

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{incidentId}/activities");

        var items = body.GetProperty("items").EnumerateArray().ToList();

        // Urutan jenis kejadian sama persis dengan urutan terjadinya
        Assert.Equal(
            new[] { "Created", "StatusChanged", "CommentAdded", "Resolved", "Closed" },
            items.Select(i => i.GetProperty("type").GetString()));

        // Waktunya naik terus (kronologis)
        var times = items.Select(i => i.GetProperty("createdAtUtc").GetDateTimeOffset()).ToList();
        Assert.Equal(times.OrderBy(t => t), times);

        // Semua dilakukan Responder
        Assert.All(items, i => Assert.Equal("Demo Responder", i.GetProperty("actorName").GetString()));
        Assert.Equal(5, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Timeline_ShowsTheAssignmentOfAnIncident()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var incidentId = await TestIncidents.CreateAsync(admin);

        await admin.PutAsJsonAsync($"/api/v1/incidents/{incidentId}/assignee",
            new { assigneeId = TestUsers.ResponderId });

        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{incidentId}/activities");

        var assigned = body.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("type").GetString() == "AssigneeChanged");
        Assert.Contains("Demo Responder", assigned.GetProperty("message").GetString());
        Assert.Equal("Demo Admin", assigned.GetProperty("actorName").GetString());
    }

    [Fact]
    public async Task Timeline_Paging_SplitsIntoPages()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);        // 1 activity: Created
        await TestIncidents.AddCommentAsync(client, incidentId, "A");    // 2
        await TestIncidents.AddCommentAsync(client, incidentId, "B");    // 3

        var page1 = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{incidentId}/activities?pageSize=2&page=1");
        var page2 = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{incidentId}/activities?pageSize=2&page=2");

        Assert.Equal(2, page1.GetProperty("items").GetArrayLength());
        Assert.Equal(1, page2.GetProperty("items").GetArrayLength());
        Assert.Equal(3, page1.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page1.GetProperty("totalPges").GetInt32());
    }

    [Fact]
    public async Task List_OnAnotherTeamsIncident_Returns404()
    {
        var otherTeam = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamResponder);
        var foreignId = await TestIncidents.CreateAsync(otherTeam);
        var platform = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);

        var response = await platform.GetAsync($"/api/v1/incidents/{foreignId}/activities");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_WithInvalidPaging_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);

        var response = await client.GetAsync($"/api/v1/incidents/{incidentId}/activities?pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ThereIsNoWayToEditOrDeleteActivities()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var incidentId = await TestIncidents.CreateAsync(client);

        var post = await client.PostAsJsonAsync($"/api/v1/incidents/{incidentId}/activities", new { message = "Palsu" });
        var delete = await client.DeleteAsync($"/api/v1/incidents/{incidentId}/activities");

        // Timeline hanya bisa DIBACA. Method lain: 404 atau 405
        foreach (var response in new[] { post, delete })
        {
            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                $"Expected 404 or 405 but got {(int)response.StatusCode}");
        }

        // Timeline tetap utuh: hanya catatan "Created"
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{incidentId}/activities");
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
    }
}