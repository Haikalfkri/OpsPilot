using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

/// <summary>
/// Setiap tes memakai judul unik (berisi GUID) supaya tidak bercampur dengan data tes lain,
/// karena semua tes berbagi satu database.
/// </summary>
[Collection("api")]
public class IncidentEndpointTests(ApiFactory factory)
{
    // ==================== Helper ====================

    private static string UniqueTitle(string prefix = "Incident") => $"{prefix} {Guid.NewGuid():N}";

    /// <summary>Membuat incident lewat API dan mengembalikan JSON detailnya.</summary>
    private static async Task<JsonElement> CreateAsync(
        HttpClient client, string? title = null, string severity = "High", Guid? assigneeId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/incidents", new
        {
            title = title ?? UniqueTitle(),
            description = "Deskripsi untuk pengujian",
            severity,
            assigneeId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(HttpClient client, Guid id, string status) =>
        client.PostAsJsonAsync($"/api/v1/incidents/{id}/status", new { status });

    /// <summary>Membaca langsung dari database: untuk memeriksa activity yang tidak punya endpoint (Phase 5).</summary>
    private async Task<List<IncidentActivity>> GetActivitiesAsync(Guid incidentId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpsPilotDbContext>();

        return await db.IncidentActivities.AsNoTracking()
            .Where(a => a.IncidentId == incidentId)
            .ToListAsync();
    }

    // ==================== 401 dan 403 ====================

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/incidents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/incidents",
            new { title = UniqueTitle(), description = "x", severity = "Low" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EveryWriteEndpoint_AsViewer_Returns403_EvenThoughViewerCanRead()
    {
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var viewer = await factory.CreateAuthenticatedClientAsync(TestUsers.Viewer);
        var incident = await CreateAsync(responder);
        var id = incident.GetProperty("id").GetGuid();

        var create = await viewer.PostAsJsonAsync("/api/v1/incidents",
            new { title = UniqueTitle(), description = "x", severity = "Low" });
        var update = await viewer.PatchJsonAsync($"/api/v1/incidents/{id}", new { title = UniqueTitle() });
        var status = await ChangeStatusAsync(viewer, id, "Investigating");
        var assign = await viewer.PutAsJsonAsync($"/api/v1/incidents/{id}/assignee", new { assigneeId = TestUsers.ResponderId });

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);

        // Viewer tetap boleh MEMBACA
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/v1/incidents/{id}")).StatusCode);
    }

    // ==================== POST /incidents ====================

    [Fact]
    public async Task Create_AsResponder_Returns201_Open_WithSlaTargetsAndLocationHeader()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var response = await client.PostAsJsonAsync("/api/v1/incidents",
            new { title = UniqueTitle(), description = "Server tidak merespons", severity = "Critical" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.ReadJsonAsync();
        Assert.Equal("Open", body.GetProperty("status").GetString());
        Assert.Equal("Critical", body.GetProperty("severity").GetString());

        // Critical: respons 15 menit, penyelesaian 4 jam (dihitung dari waktu dibuat)
        var created = body.GetProperty("createdAtUtc").GetDateTimeOffset();
        Assert.Equal(TimeSpan.FromMinutes(15), body.GetProperty("responseDueAtUtc").GetDateTimeOffset() - created);
        Assert.Equal(TimeSpan.FromHours(4), body.GetProperty("resolutionDueAtUtc").GetDateTimeOffset() - created);

        Assert.Equal("Demo Responder", body.GetProperty("createdByName").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("firstRespondedAtUtc").ValueKind);
    }

    [Fact]
    public async Task Create_IgnoresTeamIdAndCreatedByFromTheClient()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");

        // Client mencoba memasukkan incident ke tim lain, atas nama user lain
        var response = await client.PostAsJsonAsync("/api/v1/incidents", new
        {
            title = UniqueTitle(),
            description = "Percobaan",
            severity = "Low",
            teamId = Guid.NewGuid(),
            createdById = TestUsers.AdminId
        });

        var body = await response.ReadJsonAsync();
        Assert.Equal(me.GetProperty("teamId").GetGuid(), body.GetProperty("teamId").GetGuid());
        Assert.Equal(TestUsers.ResponderId, body.GetProperty("createdById").GetGuid());
    }

    [Fact]
    public async Task Create_WithInvalidBody_Returns400WithFieldErrors()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        // Judul terlalu pendek, deskripsi kosong, severity tidak dikirim
        var response = await client.PostAsJsonAsync("/api/v1/incidents",
            new { title = "abc", description = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("description", out _));
        Assert.True(errors.TryGetProperty("severity", out _));
    }

    [Fact]
    public async Task Create_WithValidAssignee_SetsAssigneeName_AndRecordsTwoActivities()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);

        var body = await CreateAsync(client, assigneeId: TestUsers.ResponderId);

        Assert.Equal(TestUsers.ResponderId, body.GetProperty("assigneeId").GetGuid());
        Assert.Equal("Demo Responder", body.GetProperty("assigneeName").GetString());

        var activities = await GetActivitiesAsync(body.GetProperty("id").GetGuid());
        Assert.Equal(2, activities.Count);
        Assert.Contains(activities, a => a.Type == ActivityType.Created);
        Assert.Contains(activities, a => a.Type == ActivityType.AssigneeChanged);
    }

    [Fact]
    public async Task Create_WithoutAnAssignee_RecordsExactlyOneCreatedActivity()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var body = await CreateAsync(client);

        var activities = await GetActivitiesAsync(body.GetProperty("id").GetGuid());
        Assert.Single(activities);
        Assert.Equal(ActivityType.Created, activities[0].Type);
        Assert.Equal(TestUsers.ResponderId, activities[0].ActorId);
    }

    [Fact]
    public async Task Create_WithAnAssigneeWhoCannotBeAssigned_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        // Viewer, user nonaktif, user tim lain, dan id yang tidak ada
        foreach (var badAssignee in new[]
                 { TestUsers.ViewerId, TestUsers.InactiveId, TestUsers.OtherTeamResponderId, Guid.NewGuid() })
        {
            var response = await client.PostAsJsonAsync("/api/v1/incidents", new
            {
                title = UniqueTitle(),
                description = "x",
                severity = "Low",
                assigneeId = badAssignee
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errors = (await response.ReadJsonAsync()).GetProperty("errors");
            Assert.True(errors.TryGetProperty("assigneeId", out _));
        }
    }

    // ==================== GET /incidents/{id} ====================

    [Fact]
    public async Task GetById_ReturnsTheDetail_AndStartsWithTheOpenTransitions()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var created = await CreateAsync(client);
        var id = created.GetProperty("id").GetGuid();

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{id}");

        Assert.Equal(created.GetProperty("title").GetString(), body.GetProperty("title").GetString());
        var next = body.GetProperty("allowedNextStatuses").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Equal(new[] { "Investigating", "Resolved" }, next);
    }

    [Fact]
    public async Task GetById_OfAnotherTeam_Returns404_NotForbidden()
    {
        var otherTeam = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamResponder);
        var foreign = await CreateAsync(otherTeam);
        var platform = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);

        var response = await platform.GetAsync($"/api/v1/incidents/{foreign.GetProperty("id").GetGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUnknownId_Returns404()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var response = await client.GetAsync($"/api/v1/incidents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EveryWriteEndpoint_OnAnotherTeamsIncident_Returns404()
    {
        var otherTeam = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamResponder);
        var foreignId = (await CreateAsync(otherTeam)).GetProperty("id").GetGuid();
        var platform = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var update = await platform.PatchJsonAsync($"/api/v1/incidents/{foreignId}", new { title = UniqueTitle() });
        var status = await ChangeStatusAsync(platform, foreignId, "Investigating");
        var assign = await platform.PutAsJsonAsync($"/api/v1/incidents/{foreignId}/assignee",
            new { assigneeId = TestUsers.ResponderId });

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, assign.StatusCode);
    }

    // ==================== GET /incidents (daftar) ====================

    /// <summary>Membuat 3 incident (Low, Medium, Critical) dengan kata kunci yang sama.</summary>
    private static async Task<string> SeedThreeAsync(HttpClient client)
    {
        var token = Guid.NewGuid().ToString("N");
        await CreateAsync(client, $"Seed Low {token}", "Low");
        await CreateAsync(client, $"Seed Medium {token}", "Medium");
        await CreateAsync(client, $"Seed Critical {token}", "Critical");
        return token;
    }

    [Fact]
    public async Task List_SearchFindsTheTitle_AndOtherTeamsIncidentsNeverAppear()
    {
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var otherTeam = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamResponder);

        var token = await SeedThreeAsync(responder);
        await CreateAsync(otherTeam, $"Foreign {token}");   // kata kunci sama, tapi tim lain

        var body = await responder.GetFromJsonAsync<JsonElement>($"/api/v1/incidents?search={token}");

        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
        var titles = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString());
        Assert.DoesNotContain(titles, t => t!.StartsWith("Foreign"));
    }

    [Fact]
    public async Task List_FiltersBySeverity()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var token = await SeedThreeAsync(client);

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents?search={token}&severity=Critical");

        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal("Critical", body.GetProperty("items")[0].GetProperty("severity").GetString());
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var token = Guid.NewGuid().ToString("N");
        var first = await CreateAsync(client, $"Status A {token}");
        await CreateAsync(client, $"Status B {token}");
        await ChangeStatusAsync(client, first.GetProperty("id").GetGuid(), "Investigating");

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents?search={token}&status=Investigating");

        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task List_FiltersByAssignee()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var token = Guid.NewGuid().ToString("N");
        await CreateAsync(client, $"Assigned {token}", assigneeId: TestUsers.ResponderId);
        await CreateAsync(client, $"Unassigned {token}");

        var body = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/incidents?search={token}&assigneeId={TestUsers.ResponderId}");

        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal("Demo Responder", body.GetProperty("items")[0].GetProperty("assigneeName").GetString());
    }

    [Fact]
    public async Task List_FiltersByDateRange()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var token = await SeedThreeAsync(client);

        var future = Uri.EscapeDataString("2999-01-01T00:00:00Z");
        var past = Uri.EscapeDataString("2000-01-01T00:00:00Z");

        var none = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents?search={token}&createdFrom={future}");
        var all = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents?search={token}&createdFrom={past}");

        Assert.Equal(0, none.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, all.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task List_SortedBySeverity_UsesSeverityOrder_NotAlphabeticalOrder()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var token = await SeedThreeAsync(client);

        var desc = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents?search={token}&sortBy=severity&sortDirection=desc");
        var asc = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents?search={token}&sortBy=severity&sortDirection=asc");

        string[] Severities(JsonElement body) => body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("severity").GetString()!).ToArray();

        Assert.Equal(new[] { "Critical", "Medium", "Low" }, Severities(desc));
        Assert.Equal(new[] { "Low", "Medium", "Critical" }, Severities(asc));
    }

    [Fact]
    public async Task List_DefaultSort_IsNewestFirst()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var token = Guid.NewGuid().ToString("N");
        await CreateAsync(client, $"First {token}");
        await Task.Delay(20);   // pastikan waktu dibuat berbeda
        await CreateAsync(client, $"Second {token}");

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents?search={token}");

        Assert.StartsWith("Second", body.GetProperty("items")[0].GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("pageSize=1000")]
    [InlineData("page=0")]
    [InlineData("sortBy=password")]
    [InlineData("sortDirection=sideways")]
    [InlineData("status=Bogus")]
    public async Task List_WithInvalidParameters_Returns400(string queryString)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var response = await client.GetAsync($"/api/v1/incidents?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ==================== PATCH /incidents/{id} ====================

    [Fact]
    public async Task Update_ChangesTitleAndDescription_AndRecordsAnUpdatedActivity()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();
        var newTitle = UniqueTitle("Judul baru");

        var response = await client.PatchJsonAsync($"/api/v1/incidents/{id}",
            new { title = newTitle, description = "Deskripsi baru" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(newTitle, body.GetProperty("title").GetString());
        Assert.Equal("Deskripsi baru", body.GetProperty("description").GetString());
        Assert.Equal("High", body.GetProperty("severity").GetString());   // tidak dikirim, tidak berubah

        Assert.Contains(await GetActivitiesAsync(id), a => a.Type == ActivityType.Updated);
    }

    [Fact]
    public async Task Update_Severity_RecalculatesSlaFromTheCreationTime()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = (await CreateAsync(client, severity: "Low")).GetProperty("id").GetGuid();

        var response = await client.PatchJsonAsync($"/api/v1/incidents/{id}", new { severity = "Critical" });

        var body = await response.ReadJsonAsync();
        var created = body.GetProperty("createdAtUtc").GetDateTimeOffset();
        Assert.Equal(TimeSpan.FromMinutes(15), body.GetProperty("responseDueAtUtc").GetDateTimeOffset() - created);
        Assert.Equal(TimeSpan.FromHours(4), body.GetProperty("resolutionDueAtUtc").GetDateTimeOffset() - created);

        Assert.Contains(await GetActivitiesAsync(id), a => a.Type == ActivityType.SeverityChanged);
    }

    [Fact]
    public async Task Update_WithEmptyBody_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();

        var response = await client.PatchJsonAsync($"/api/v1/incidents/{id}", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_AClosedIncident_Returns409()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();
        await ChangeStatusAsync(client, id, "Resolved");
        await ChangeStatusAsync(client, id, "Closed");

        var response = await client.PatchJsonAsync($"/api/v1/incidents/{id}", new { title = UniqueTitle() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ==================== POST /incidents/{id}/status ====================

    [Fact]
    public async Task ChangeStatus_FullLifecycle_SetsTimestamps_AndRecordsActivities()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();

        var investigating = await (await ChangeStatusAsync(client, id, "Investigating")).ReadJsonAsync();
        Assert.Equal("Investigating", investigating.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, investigating.GetProperty("firstRespondedAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, investigating.GetProperty("resolvedAtUtc").ValueKind);

        var resolved = await (await ChangeStatusAsync(client, id, "Resolved")).ReadJsonAsync();
        Assert.NotEqual(JsonValueKind.Null, resolved.GetProperty("resolvedAtUtc").ValueKind);

        var closed = await (await ChangeStatusAsync(client, id, "Closed")).ReadJsonAsync();
        Assert.NotEqual(JsonValueKind.Null, closed.GetProperty("closedAtUtc").ValueKind);
        Assert.Empty(closed.GetProperty("allowedNextStatuses").EnumerateArray());   // Closed = status akhir

        var types = (await GetActivitiesAsync(id)).Select(a => a.Type).ToList();
        Assert.Contains(ActivityType.Created, types);
        Assert.Contains(ActivityType.StatusChanged, types);
        Assert.Contains(ActivityType.Resolved, types);
        Assert.Contains(ActivityType.Closed, types);
    }

    [Fact]
    public async Task ChangeStatus_OpenDirectlyToResolved_IsAllowed()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();

        var response = await ChangeStatusAsync(client, id, "Resolved");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Open")]          // Open -> Open
    [InlineData("Closed")]        // Open -> Closed (melompati Resolved)
    public async Task ChangeStatus_WithIllegalTransition_Returns409_AndStatusStaysTheSame(string target)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();

        var response = await ChangeStatusAsync(client, id, target);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var current = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{id}");
        Assert.Equal("Open", current.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ChangeStatus_OutOfClosed_Returns409()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();
        await ChangeStatusAsync(client, id, "Resolved");
        await ChangeStatusAsync(client, id, "Closed");

        var response = await ChangeStatusAsync(client, id, "Investigating");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithMissingOrUnknownStatus_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();

        var missing = await client.PostAsJsonAsync($"/api/v1/incidents/{id}/status", new { });
        var unknown = await ChangeStatusAsync(client, id, "Bogus");

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    // ==================== PUT /incidents/{id}/assignee ====================

    [Fact]
    public async Task Assign_ThenUnassign_Works()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var id = (await CreateAsync(admin)).GetProperty("id").GetGuid();

        var assigned = await (await admin.PutAsJsonAsync($"/api/v1/incidents/{id}/assignee",
            new { assigneeId = TestUsers.ResponderId })).ReadJsonAsync();

        Assert.Equal(TestUsers.ResponderId, assigned.GetProperty("assigneeId").GetGuid());
        Assert.Equal("Demo Responder", assigned.GetProperty("assigneeName").GetString());

        var unassigned = await (await admin.PutAsJsonAsync($"/api/v1/incidents/{id}/assignee",
            new { assigneeId = (Guid?)null })).ReadJsonAsync();

        Assert.Equal(JsonValueKind.Null, unassigned.GetProperty("assigneeId").ValueKind);
    }

    [Fact]
    public async Task Assign_TheSameAssigneeTwice_DoesNotDuplicateTheActivity()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var id = (await CreateAsync(admin)).GetProperty("id").GetGuid();
        var body = new { assigneeId = TestUsers.ResponderId };

        await admin.PutAsJsonAsync($"/api/v1/incidents/{id}/assignee", body);
        var second = await admin.PutAsJsonAsync($"/api/v1/incidents/{id}/assignee", body);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var assignments = (await GetActivitiesAsync(id)).Count(a => a.Type == ActivityType.AssigneeChanged);
        Assert.Equal(1, assignments);
    }

    [Fact]
    public async Task Assign_ToAnInvalidUser_Returns400_AndNothingChanges()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var id = (await CreateAsync(admin)).GetProperty("id").GetGuid();

        foreach (var badAssignee in new[]
                 { TestUsers.ViewerId, TestUsers.InactiveId, TestUsers.OtherTeamResponderId, Guid.NewGuid() })
        {
            var response = await admin.PutAsJsonAsync($"/api/v1/incidents/{id}/assignee", new { assigneeId = badAssignee });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var current = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{id}");
        Assert.Equal(JsonValueKind.Null, current.GetProperty("assigneeId").ValueKind);
    }
}