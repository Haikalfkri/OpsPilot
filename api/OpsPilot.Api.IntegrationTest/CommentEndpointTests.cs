using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class CommentEndpointTests(ApiFactory factory)
{
    // ==================== 401 dan 403 ====================

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync($"/api/v1/incidents/{Guid.NewGuid()}/comments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Add_WithoutToken_Returns401()
    {
        var response = await TestIncidents.AddCommentAsync(factory.CreateClient(), Guid.NewGuid(), "Halo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CanReadComments_ButCannotAddThem()
    {
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var viewer = await factory.CreateAuthenticatedClientAsync(TestUsers.Viewer);
        var incidentId = await TestIncidents.CreateAsync(responder);

        var add = await TestIncidents.AddCommentAsync(viewer, incidentId, "Saya Viewer");
        var list = await viewer.GetAsync($"/api/v1/incidents/{incidentId}/comments");

        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    // ==================== POST /comments ====================

    [Fact]
    public async Task Add_AsResponder_Returns201_WithAuthorAndTimestamp()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);

        var response = await TestIncidents.AddCommentAsync(client, incidentId, "Sedang dicek, mohon tunggu.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.ReadJsonAsync();
        Assert.Equal("Sedang dicek, mohon tunggu.", body.GetProperty("body").GetString());
        Assert.Equal(TestUsers.ResponderId, body.GetProperty("authorId").GetGuid());
        Assert.Equal("Demo Responder", body.GetProperty("authorName").GetString());
        Assert.Equal(incidentId, body.GetProperty("incidentId").GetGuid());
        Assert.True(body.GetProperty("createdAtUtc").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Add_AsAdmin_Works()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var incidentId = await TestIncidents.CreateAsync(client);

        var response = await TestIncidents.AddCommentAsync(client, incidentId, "Komentar dari Admin");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Add_IgnoresAnAuthorIdSentByTheClient()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);

        // Client mencoba menulis komentar atas nama Admin
        var response = await client.PostAsJsonAsync($"/api/v1/incidents/{incidentId}/comments",
            new { body = "Percobaan menyamar", authorId = TestUsers.AdminId });

        var body = await response.ReadJsonAsync();
        Assert.Equal(TestUsers.ResponderId, body.GetProperty("authorId").GetGuid());
    }

    [Fact]
    public async Task Add_ThenReload_TheCommentIsStillThere()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);
        await TestIncidents.AddCommentAsync(client, incidentId, "Komentar yang harus tetap ada");

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{incidentId}/comments");

        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.Equal("Komentar yang harus tetap ada", list.GetProperty("items")[0].GetProperty("body").GetString());
        Assert.Equal("Demo Responder", list.GetProperty("items")[0].GetProperty("authorName").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Add_WithEmptyBody_Returns400WithFieldError(string body)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);

        var response = await TestIncidents.AddCommentAsync(client, incidentId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("body", out _));
    }

    [Fact]
    public async Task Add_WithTooLongBody_Returns400_ButExactlyTheLimitIsAccepted()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);

        var tooLong = await TestIncidents.AddCommentAsync(client, incidentId, new string('a', 2001));
        var atLimit = await TestIncidents.AddCommentAsync(client, incidentId, new string('a', 2000));

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);
    }

    [Fact]
    public async Task Add_WithoutABodyField_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/incidents/{incidentId}/comments", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Add_OnAClosedIncident_IsAllowed()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);
        await TestIncidents.ChangeStatusAsync(client, incidentId, "Resolved");
        await TestIncidents.ChangeStatusAsync(client, incidentId, "Closed");

        var response = await TestIncidents.AddCommentAsync(client, incidentId, "Tindak lanjut setelah ditutup");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ==================== Isolasi tim ====================

    [Fact]
    public async Task EveryCommentEndpoint_OnAnotherTeamsIncident_Returns404()
    {
        var otherTeam = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamResponder);
        var foreignId = await TestIncidents.CreateAsync(otherTeam);
        var platform = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var list = await platform.GetAsync($"/api/v1/incidents/{foreignId}/comments");
        var add = await TestIncidents.AddCommentAsync(platform, foreignId, "Percobaan");

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
    }

    [Fact]
    public async Task EveryCommentEndpoint_OnAnUnknownIncident_Returns404()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var list = await client.GetAsync($"/api/v1/incidents/{Guid.NewGuid()}/comments");
        var add = await TestIncidents.AddCommentAsync(client, Guid.NewGuid(), "Halo");

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
    }

    // ==================== GET /comments: urutan dan paging ====================

    [Fact]
    public async Task List_IsOldestFirst_AndPagingSplitsIntoPages()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);
        await TestIncidents.AddCommentAsync(client, incidentId, "Komentar 1");
        await TestIncidents.AddCommentAsync(client, incidentId, "Komentar 2");
        await TestIncidents.AddCommentAsync(client, incidentId, "Komentar 3");

        var page1 = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{incidentId}/comments?pageSize=2&page=1");
        var page2 = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{incidentId}/comments?pageSize=2&page=2");

        string[] Bodies(JsonElement page) =>
            page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("body").GetString()!).ToArray();

        Assert.Equal(new[] { "Komentar 1", "Komentar 2" }, Bodies(page1));
        Assert.Equal(new[] { "Komentar 3" }, Bodies(page2));
        Assert.Equal(3, page1.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page1.GetProperty("totalPges").GetInt32());
    }

    [Theory]
    [InlineData("pageSize=1000")]
    [InlineData("page=0")]
    public async Task List_WithInvalidPaging_Returns400(string queryString)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);

        var response = await client.GetAsync($"/api/v1/incidents/{incidentId}/comments?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ==================== Tidak ada edit dan hapus ====================

    [Fact]
    public async Task ThereIsNoWayToEditOrDeleteComments()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);
        var created = await (await TestIncidents.AddCommentAsync(client, incidentId, "Tidak bisa diubah")).ReadJsonAsync();
        var commentId = created.GetProperty("id").GetGuid();

        var delete = await client.DeleteAsync($"/api/v1/incidents/{incidentId}/comments/{commentId}");
        var put = await client.PutAsJsonAsync($"/api/v1/incidents/{incidentId}/comments/{commentId}", new { body = "Diubah" });
        var deleteAll = await client.DeleteAsync($"/api/v1/incidents/{incidentId}/comments");

        // Alamat atau method-nya memang tidak ada: 404 atau 405
        foreach (var response in new[] { delete, put, deleteAll })
        {
            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                $"Expected 404 or 405 but got {(int)response.StatusCode}");
        }

        // Dan komentarnya tetap utuh
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{incidentId}/comments");
        Assert.Equal("Tidak bisa diubah", list.GetProperty("items")[0].GetProperty("body").GetString());
    }
}