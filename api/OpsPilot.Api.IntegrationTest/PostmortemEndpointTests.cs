using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Domain.Entities;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class PostmortemEndpointTests(ApiFactory factory)
{
    // ==================== Helper ====================

    private static string Url(Guid incidentId) => $"/api/v1/incidents/{incidentId}/postmortem";

    private static object Body(string summary = "Database berhenti karena disk penuh") => new
    {
        summary,
        rootCause = "Log tidak dirotasi",
        impact = "Layanan pembayaran terhenti 40 menit",
        correctiveActions = "Pasang rotasi log dan alarm kapasitas disk"
    };

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid incidentId, object body) =>
        client.PutAsJsonAsync(Url(incidentId), body);

    /// <summary>Membuat incident dan memindahkannya ke Resolved. Mengembalikan id-nya.</summary>
    private static async Task<Guid> CreateResolvedIncidentAsync(HttpClient client)
    {
        var id = await TestIncidents.CreateAsync(client);
        (await TestIncidents.ChangeStatusAsync(client, id, "Resolved")).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<List<JsonElement>> TimelineAsync(HttpClient client, Guid incidentId)
    {
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{incidentId}/activities?pageSize=100");
        return body.GetProperty("items").EnumerateArray().ToList();
    }

    private static List<JsonElement> OfType(List<JsonElement> items, string type) =>
        items.Where(i => i.GetProperty("type").GetString() == type).ToList();

    // ==================== 401 dan 403 ====================

    [Fact]
    public async Task EveryEndpoint_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var get = await client.GetAsync(Url(Guid.NewGuid()));
        var put = await client.PutAsJsonAsync(Url(Guid.NewGuid()), Body());

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    [Fact]
    public async Task Viewer_CanReadAPostmortem_ButCannotWriteOne()
    {
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var viewer = await factory.CreateAuthenticatedClientAsync(TestUsers.Viewer);
        var id = await CreateResolvedIncidentAsync(responder);
        await PutAsync(responder, id, Body());

        var read = await viewer.GetAsync(Url(id));
        var write = await PutAsync(viewer, id, Body("Percobaan dari Viewer"));

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

        // Isinya tidak berubah
        var current = await responder.GetFromJsonAsync<JsonElement>(Url(id));
        Assert.Equal("Database berhenti karena disk penuh", current.GetProperty("summary").GetString());
    }

    // ==================== Aturan status ====================

    [Fact]
    public async Task Put_BeforeTheIncidentIsResolved_Returns409_AndNothingIsCreated()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = await TestIncidents.CreateAsync(client);                          // Open

        var whileOpen = await PutAsync(client, id, Body());
        await TestIncidents.ChangeStatusAsync(client, id, "Investigating");
        var whileInvestigating = await PutAsync(client, id, Body());

        Assert.Equal(HttpStatusCode.Conflict, whileOpen.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, whileInvestigating.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Url(id))).StatusCode);
    }

    [Fact]
    public async Task Put_OnAClosedIncident_IsAllowed()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = await CreateResolvedIncidentAsync(client);
        await TestIncidents.ChangeStatusAsync(client, id, "Closed");

        var response = await PutAsync(client, id, Body());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ==================== Validasi ====================

    [Fact]
    public async Task Put_WithAnInvalidBody_Returns400WithFieldErrors()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = await CreateResolvedIncidentAsync(client);

        // summary kosong, rootCause terlalu panjang, impact dan correctiveActions tidak dikirim
        var response = await PutAsync(client, id, new { summary = "", rootCause = new string('a', 4001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("summary", out _));
        Assert.True(errors.TryGetProperty("rootCause", out _));
        Assert.True(errors.TryGetProperty("impact", out _));
        Assert.True(errors.TryGetProperty("correctiveActions", out _));
    }

    [Fact]
    public async Task Put_ChecksTheBodyBeforeTheStatus_SoABadBodyOnAnOpenIncidentIs400_Not409()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = await TestIncidents.CreateAsync(client);                          // masih Open

        var response = await PutAsync(client, id, new { summary = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ==================== Membuat ====================

    [Fact]
    public async Task Get_BeforeAnyPostmortemExists_Returns404()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = await CreateResolvedIncidentAsync(client);

        var response = await client.GetAsync(Url(id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_TheFirstTime_Returns201_WithTheAuthorFromTheToken_AndLocationHeader()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = await CreateResolvedIncidentAsync(client);

        var response = await PutAsync(client, id, Body());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith("/postmortem", response.Headers.Location!.ToString());

        var body = await response.ReadJsonAsync();
        Assert.Equal(id, body.GetProperty("incidentId").GetGuid());
        Assert.Equal(TestUsers.ResponderId, body.GetProperty("authorId").GetGuid());
        Assert.Equal("Demo Responder", body.GetProperty("authorName").GetString());
        Assert.Equal("Database berhenti karena disk penuh", body.GetProperty("summary").GetString());
        Assert.Equal("Log tidak dirotasi", body.GetProperty("rootCause").GetString());
        Assert.Equal("Layanan pembayaran terhenti 40 menit", body.GetProperty("impact").GetString());
        Assert.Equal("Pasang rotasi log dan alarm kapasitas disk", body.GetProperty("correctiveActions").GetString());

        // Dan tersimpan: dibaca ulang lewat GET (tetap ada setelah "reload")
        var reloaded = await client.GetFromJsonAsync<JsonElement>(Url(id));
        Assert.Equal(body.GetProperty("id").GetGuid(), reloaded.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Put_IgnoresAnAuthorIdSentByTheClient()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = await CreateResolvedIncidentAsync(client);

        // Client mencoba menulis atas nama Admin
        var response = await PutAsync(client, id, new
        {
            summary = "Ringkasan",
            rootCause = "Akar masalah",
            impact = "Dampak",
            correctiveActions = "Tindakan",
            authorId = TestUsers.AdminId
        });

        var body = await response.ReadJsonAsync();
        Assert.Equal(TestUsers.ResponderId, body.GetProperty("authorId").GetGuid());
    }

    // ==================== Memperbarui ====================

    [Fact]
    public async Task Put_Again_Returns200_AndKeepsTheOriginalAuthorAndCreationTime()
    {
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var id = await CreateResolvedIncidentAsync(responder);
        await PutAsync(responder, id, Body());

        var before = await responder.GetFromJsonAsync<JsonElement>(Url(id));
        await Task.Delay(20);                                                     // pastikan waktunya berbeda

        var response = await PutAsync(admin, id, Body("Ringkasan yang diperbarui Admin"));      // Admin memperbarui

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await responder.GetFromJsonAsync<JsonElement>(Url(id));
        Assert.Equal("Ringkasan yang diperbarui Admin", after.GetProperty("summary").GetString());
        Assert.Equal(before.GetProperty("id").GetGuid(), after.GetProperty("id").GetGuid());             // postmortem yang sama
        Assert.Equal(TestUsers.ResponderId, after.GetProperty("authorId").GetGuid());                    // penulis asli tetap
        Assert.Equal(before.GetProperty("createdAtUtc").GetDateTimeOffset(), after.GetProperty("createdAtUtc").GetDateTimeOffset());
        Assert.True(after.GetProperty("updatedAtUtc").GetDateTimeOffset() > before.GetProperty("updatedAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task Put_WithTheSameContent_Returns200_ChangesNothing_AndAddsNoActivity()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = await CreateResolvedIncidentAsync(client);
        await PutAsync(client, id, Body());

        var before = await client.GetFromJsonAsync<JsonElement>(Url(id));
        await Task.Delay(20);

        var response = await PutAsync(client, id, Body());                        // isi persis sama

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await client.GetFromJsonAsync<JsonElement>(Url(id));
        var difference = (after.GetProperty("updatedAtUtc").GetDateTimeOffset()
                          - before.GetProperty("updatedAtUtc").GetDateTimeOffset()).Duration();
        Assert.True(difference < TimeSpan.FromMilliseconds(1), "UpdatedAt must not move when nothing changed.");

        var timeline = await TimelineAsync(client, id);
        Assert.Single(OfType(timeline, "PostmortemCreated"));
        Assert.Empty(OfType(timeline, "PostmortemUpdated"));
    }

    // ==================== Timeline ====================

    [Fact]
    public async Task Timeline_ShowsWhoCreatedAndWhoUpdatedThePostmortem()
    {
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var id = await CreateResolvedIncidentAsync(responder);

        await PutAsync(responder, id, Body());
        await PutAsync(admin, id, Body("Versi kedua dari Admin"));

        var timeline = await TimelineAsync(responder, id);

        var created = Assert.Single(OfType(timeline, "PostmortemCreated"));
        var updated = Assert.Single(OfType(timeline, "PostmortemUpdated"));
        Assert.Equal("Demo Responder", created.GetProperty("actorName").GetString());
        Assert.Equal("Demo Admin", updated.GetProperty("actorName").GetString());
    }

    // ==================== Isolasi tim ====================

    [Fact]
    public async Task AnotherTeam_CannotReadOrWriteThePostmortem_Returns404()
    {
        var otherTeam = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamResponder);
        var platform = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var foreignId = await CreateResolvedIncidentAsync(otherTeam);
        await PutAsync(otherTeam, foreignId, Body("Rahasia tim lain"));

        var read = await platform.GetAsync(Url(foreignId));
        var write = await PutAsync(platform, foreignId, Body("Penyusup"));

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);

        // Milik tim lain tidak tersentuh
        var original = await otherTeam.GetFromJsonAsync<JsonElement>(Url(foreignId));
        Assert.Equal("Rahasia tim lain", original.GetProperty("summary").GetString());
    }

    [Fact]
    public async Task UnknownIncident_Returns404_ForBothEndpoints()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var get = await client.GetAsync(Url(Guid.NewGuid()));
        var put = await PutAsync(client, Guid.NewGuid(), Body());

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
    }

    // ==================== Satu postmortem per incident ====================

    [Fact]
    public async Task TwoSimultaneousFirstWrites_CreateExactlyOnePostmortem_AndNeverFailWithA500()
    {
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var id = await CreateResolvedIncidentAsync(responder);

        // Dua orang menyimpan postmortem pertama pada saat yang sama
        var results = await Task.WhenAll(
            PutAsync(responder, id, Body("Dari Responder")),
            PutAsync(admin, id, Body("Dari Admin")));

        var codes = results.Select(r => r.StatusCode).ToList();

        // Persis satu yang berhasil membuat. Yang lain: 200 (datang sesudahnya, jadi memperbarui)
        // atau 409 (bertabrakan saat menyimpan). Tidak pernah 500.
        Assert.Equal(1, codes.Count(c => c == HttpStatusCode.Created));
        Assert.All(codes.Where(c => c != HttpStatusCode.Created),
            c => Assert.True(c is HttpStatusCode.OK or HttpStatusCode.Conflict, $"Unexpected status {(int)c}"));

        Assert.Equal(HttpStatusCode.OK, (await responder.GetAsync(Url(id))).StatusCode);
        Assert.Single(OfType(await TimelineAsync(responder, id), "PostmortemCreated"));
    }

    [Fact]
    public async Task TheDatabase_RejectsASecondPostmortemForTheSameIncident()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var id = await CreateResolvedIncidentAsync(client);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpsPilotDbContext>();
        var now = DateTimeOffset.UtcNow;

        db.Postmortems.Add(Postmortem.Create(id, TestUsers.ResponderId, "s", "r", "i", "c", now));
        await db.SaveChangesAsync();

        // Pengaman terakhir: index unik di PostgreSQL menolak postmortem kedua untuk incident yang sama
        db.Postmortems.Add(Postmortem.Create(id, TestUsers.ResponderId, "s2", "r2", "i2", "c2", now));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // ==================== Tidak ada hapus ====================

    [Fact]
    public async Task ThereIsNoWayToDeleteOrPostAPostmortem()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var id = await CreateResolvedIncidentAsync(client);
        await PutAsync(client, id, Body());

        var delete = await client.DeleteAsync(Url(id));
        var post = await client.PostAsJsonAsync(Url(id), Body());

        // Alamat ada (untuk GET dan PUT), tapi method lain tidak: 404 atau 405
        foreach (var response in new[] { delete, post })
        {
            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                $"Expected 404 or 405 but got {(int)response.StatusCode}");
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Url(id))).StatusCode);   // masih utuh
    }
}