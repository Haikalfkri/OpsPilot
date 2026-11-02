using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class NotificationEndpointTests(ApiFactory factory)
{
    // ==================== Helper ====================

    private static async Task AssignAsync(HttpClient client, Guid incidentId, Guid assigneeId)
    {
        var response = await client.PutAsJsonAsync($"/api/v1/incidents/{incidentId}/assignee", new { assigneeId });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<List<JsonElement>> ListAsync(HttpClient client, string query = "pageSize=100")
    {
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/notifications?{query}");
        return body.GetProperty("items").EnumerateArray().ToList();
    }

    /// <summary>Hanya notifikasi yang terkait incident tertentu (supaya tidak terganggu data tes lain).</summary>
    private static List<JsonElement> ForIncident(List<JsonElement> items, Guid incidentId) =>
        items.Where(i =>
                i.GetProperty("incidentId").ValueKind == JsonValueKind.String &&
                i.GetProperty("incidentId").GetGuid() == incidentId)
            .ToList();

    private static async Task<int> UnreadCountAsync(HttpClient client)
    {
        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count");
        return body.GetProperty("count").GetInt32();
    }

    private static Task<HttpResponseMessage> MarkReadAsync(HttpClient client, Guid notificationId) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/notifications/{notificationId}/read"));

    // ==================== 401 ====================

    [Fact]
    public async Task EveryEndpoint_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var list = await client.GetAsync("/api/v1/notifications");
        var count = await client.GetAsync("/api/v1/notifications/unread-count");
        var read = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/notifications/{Guid.NewGuid()}/read"));
        var readAll = await client.PostAsync("/api/v1/notifications/read-all", null);

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, count.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, readAll.StatusCode);
    }

    // ==================== Notifikasi saat assign ====================

    [Fact]
    public async Task Assign_CreatesANotificationForTheAssignee_WithTheRightContent_AndNotForTheAssigner()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(admin);

        await AssignAsync(admin, incidentId, TestUsers.ResponderId);

        var mine = Assert.Single(ForIncident(await ListAsync(responder), incidentId));
        Assert.Equal("IncidentAssigned", mine.GetProperty("type").GetString());
        Assert.False(mine.GetProperty("isRead").GetBoolean());
        Assert.Equal(JsonValueKind.Null, mine.GetProperty("readAtUtc").ValueKind);
        Assert.Contains("Demo Admin", mine.GetProperty("message").GetString());

        // Yang meng-assign tidak mendapat notifikasi untuk incident ini
        Assert.Empty(ForIncident(await ListAsync(admin), incidentId));
    }

    [Fact]
    public async Task Create_WithAnAssignee_AlsoNotifiesThem()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var response = await admin.PostAsJsonAsync("/api/v1/incidents", new
        {
            title = $"Notif test {Guid.NewGuid():N}",
            description = "Dibuat dengan assignee",
            severity = "High",
            assigneeId = TestUsers.ResponderId
        });
        var incidentId = (await response.ReadJsonAsync()).GetProperty("id").GetGuid();

        Assert.Single(ForIncident(await ListAsync(responder), incidentId));
    }

    [Fact]
    public async Task Assign_ToMyself_CreatesNoNotification()
    {
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(responder);

        await AssignAsync(responder, incidentId, TestUsers.ResponderId);

        Assert.Empty(ForIncident(await ListAsync(responder), incidentId));
    }

    [Fact]
    public async Task Assign_ThenUnassign_DoesNotCreateASecondNotification()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(admin);

        await AssignAsync(admin, incidentId, TestUsers.ResponderId);
        await admin.PutAsJsonAsync($"/api/v1/incidents/{incidentId}/assignee", new { assigneeId = (Guid?)null });
        await AssignAsync(admin, incidentId, TestUsers.ResponderId);    // di-assign lagi: notifikasi baru

        // Dua kali di-assign = dua notifikasi. Melepas assignee tidak menambah apa pun.
        Assert.Equal(2, ForIncident(await ListAsync(responder), incidentId).Count);
    }

    // ==================== Jumlah belum dibaca dan tandai dibaca ====================

    [Fact]
    public async Task UnreadCount_GoesUpByOne_AfterAnAssignment_AndDownAfterMarkingItRead()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(admin);

        var before = await UnreadCountAsync(responder);
        await AssignAsync(admin, incidentId, TestUsers.ResponderId);
        var after = await UnreadCountAsync(responder);

        Assert.Equal(before + 1, after);

        var notification = Assert.Single(ForIncident(await ListAsync(responder), incidentId));
        var response = await MarkReadAsync(responder, notification.GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await UnreadCountAsync(responder));
    }

    [Fact]
    public async Task MarkRead_SetsTheReadTime_AndCallingItAgainChangesNothing()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(admin);
        await AssignAsync(admin, incidentId, TestUsers.ResponderId);

        var id = Assert.Single(ForIncident(await ListAsync(responder), incidentId)).GetProperty("id").GetGuid();

        var first = await (await MarkReadAsync(responder, id)).ReadJsonAsync();
        var second = await (await MarkReadAsync(responder, id)).ReadJsonAsync();

        Assert.True(first.GetProperty("isRead").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, first.GetProperty("readAtUtc").ValueKind);

        // Parse back to DateTimeOffset for comparison (ignores string formatting)
        var firstTime = first.GetProperty("readAtUtc").GetDateTimeOffset();
        var secondTime = second.GetProperty("readAtUtc").GetDateTimeOffset();
        Assert.Equal(firstTime, secondTime);  // Compare as objects, not strings
    }

    [Fact]
    public async Task MarkRead_OnSomeoneElsesNotification_Returns404_AndTheNotificationStaysUnread()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(admin);
        await AssignAsync(admin, incidentId, TestUsers.ResponderId);

        var id = Assert.Single(ForIncident(await ListAsync(responder), incidentId)).GetProperty("id").GetGuid();

        // Admin mencoba menandai notifikasi milik Responder
        var response = await MarkReadAsync(admin, id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var stillThere = Assert.Single(ForIncident(await ListAsync(responder), incidentId));
        Assert.False(stillThere.GetProperty("isRead").GetBoolean());
    }

    [Fact]
    public async Task MarkRead_OnAnUnknownId_Returns404()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var response = await MarkReadAsync(client, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ==================== Tandai semua ====================

    [Fact]
    public async Task ReadAll_MarksEverythingOfTheCaller_ButNobodyElses()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        // Responder mendapat satu notifikasi, Admin juga mendapat satu (di-assign oleh Responder)
        var forResponder = await TestIncidents.CreateAsync(admin);
        await AssignAsync(admin, forResponder, TestUsers.ResponderId);
        var forAdmin = await TestIncidents.CreateAsync(responder);
        await AssignAsync(responder, forAdmin, TestUsers.AdminId);

        var adminUnreadBefore = await UnreadCountAsync(admin);
        Assert.True(adminUnreadBefore >= 1);

        var response = await responder.PostAsync("/api/v1/notifications/read-all", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await UnreadCountAsync(responder));                       // semua milik Responder terbaca
        Assert.Equal(adminUnreadBefore, await UnreadCountAsync(admin));           // milik Admin tidak tersentuh
    }

    // ==================== Daftar: filter, urutan, paging ====================

    [Fact]
    public async Task List_FiltersByReadState()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentA = await TestIncidents.CreateAsync(admin);
        var incidentB = await TestIncidents.CreateAsync(admin);
        await AssignAsync(admin, incidentA, TestUsers.ResponderId);
        await AssignAsync(admin, incidentB, TestUsers.ResponderId);

        var idA = Assert.Single(ForIncident(await ListAsync(responder), incidentA)).GetProperty("id").GetGuid();
        await MarkReadAsync(responder, idA);      // A sudah dibaca, B belum

        var unread = await ListAsync(responder, "pageSize=100&isRead=false");
        var read = await ListAsync(responder, "pageSize=100&isRead=true");

        Assert.Single(ForIncident(unread, incidentB));
        Assert.Empty(ForIncident(unread, incidentA));
        Assert.Single(ForIncident(read, incidentA));
        Assert.Empty(ForIncident(read, incidentB));
    }

    [Fact]
    public async Task List_IsNewestFirst()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var older = await TestIncidents.CreateAsync(admin);
        var newer = await TestIncidents.CreateAsync(admin);

        await AssignAsync(admin, older, TestUsers.ResponderId);
        await Task.Delay(20);                                    // pastikan waktunya berbeda
        await AssignAsync(admin, newer, TestUsers.ResponderId);

        var items = await ListAsync(responder);
        var olderIndex = items.FindIndex(i => i.GetProperty("incidentId").GetGuid() == older);
        var newerIndex = items.FindIndex(i => i.GetProperty("incidentId").GetGuid() == newer);

        Assert.True(newerIndex >= 0 && olderIndex >= 0);
        Assert.True(newerIndex < olderIndex, "The newest notification should come first.");
    }

    [Theory]
    [InlineData("pageSize=1000")]
    [InlineData("page=0")]
    public async Task List_WithInvalidPaging_Returns400(string queryString)
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var response = await client.GetAsync($"/api/v1/notifications?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CanReadTheirOwnNotifications_WhichAreEmpty()
    {
        // Viewer tidak bisa di-assign dan bukan Admin, jadi tidak pernah menerima notifikasi
        var viewer = await factory.CreateAuthenticatedClientAsync(TestUsers.Viewer);

        var body = await viewer.GetFromJsonAsync<JsonElement>("/api/v1/notifications");

        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, await UnreadCountAsync(viewer));
    }
}