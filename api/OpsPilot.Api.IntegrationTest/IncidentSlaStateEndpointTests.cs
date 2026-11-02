using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class IncidentSlaStateEndpointTests(ApiFactory factory)
{
    // ==================== Helper ====================

    /// <summary>Membuat incident dengan judul unik (supaya mudah dicari di daftar).</summary>
    private static async Task<(Guid Id, string Title)> CreateAsync(HttpClient client, string severity = "High")
    {
        var title = $"SLA test {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/v1/incidents",
            new { title, description = "Dibuat untuk menguji status SLA", severity });
        response.EnsureSuccessStatusCode();

        var body = await response.ReadJsonAsync();
        return (body.GetProperty("id").GetGuid(), title);
    }

    /// <summary>Menggeser batas waktu SLA ke masa lalu, seolah-olah incident sudah lama berjalan.</summary>
    private async Task MakeDueInThePastAsync(Guid incidentId, bool response = true, bool resolution = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpsPilotDbContext>();

        var past = DateTimeOffset.UtcNow.AddHours(-1);
        var query = db.Incidents.Where(i => i.Id == incidentId);

        if (response)
            await query.ExecuteUpdateAsync(s => s.SetProperty(i => i.ResponseDueAtUtc, past));
        if (resolution)
            await query.ExecuteUpdateAsync(s => s.SetProperty(i => i.ResolutionDueAtUtc, past));
    }

    private static async Task<JsonElement> GetDetailAsync(HttpClient client, Guid id) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{id}");

    // ==================== Tes ====================

    [Fact]
    public async Task NewIncident_StartsWithBothTargetsPending()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);

        var (id, _) = await CreateAsync(client);
        var detail = await GetDetailAsync(client, id);

        Assert.Equal("Pending", detail.GetProperty("responseSla").GetString());
        Assert.Equal("Pending", detail.GetProperty("resolutionSla").GetString());
    }

    [Fact]
    public async Task OverdueAndUntouchedIncident_IsBreachedOnBothTargets()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var (id, _) = await CreateAsync(client);

        await MakeDueInThePastAsync(id);

        var detail = await GetDetailAsync(client, id);
        Assert.Equal("Breached", detail.GetProperty("responseSla").GetString());
        Assert.Equal("Breached", detail.GetProperty("resolutionSla").GetString());
    }

    [Fact]
    public async Task Investigating_MakesTheResponseTargetMet()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var (id, _) = await CreateAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/incidents/{id}/status", new { status = "Investigating" });
        var body = await response.ReadJsonAsync();

        // Dijawab langsung di response perubahan status
        Assert.Equal("Met", body.GetProperty("responseSla").GetString());
        Assert.Equal("Pending", body.GetProperty("resolutionSla").GetString());
    }

    [Fact]
    public async Task ResolvingInTime_MakesTheResolutionTargetMet()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var (id, _) = await CreateAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/incidents/{id}/status", new { status = "Resolved" });
        var body = await response.ReadJsonAsync();

        // Langsung Resolved dari Open: dihitung sebagai respons sekaligus penyelesaian
        Assert.Equal("Met", body.GetProperty("responseSla").GetString());
        Assert.Equal("Met", body.GetProperty("resolutionSla").GetString());
    }

    [Fact]
    public async Task ResolvingAfterTheDueTime_StillCountsAsBreached()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var (id, _) = await CreateAsync(client);
        await MakeDueInThePastAsync(id, response: false);    // hanya batas penyelesaian yang sudah lewat

        var response = await client.PostAsJsonAsync($"/api/v1/incidents/{id}/status", new { status = "Resolved" });
        var body = await response.ReadJsonAsync();

        Assert.Equal("Breached", body.GetProperty("resolutionSla").GetString());   // selesai, tapi terlambat
        Assert.Equal("Met", body.GetProperty("responseSla").GetString());          // batas respons masih di depan
    }

    [Fact]
    public async Task List_ShowsTheSlaStatePerIncident()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var (lateId, lateTitle) = await CreateAsync(client);
        var (freshId, freshTitle) = await CreateAsync(client);
        await MakeDueInThePastAsync(lateId);

        var late = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents?search={lateTitle}");
        var fresh = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents?search={freshTitle}");

        Assert.Equal("Breached", late.GetProperty("items")[0].GetProperty("resolutionSla").GetString());
        Assert.Equal("Pending", fresh.GetProperty("items")[0].GetProperty("resolutionSla").GetString());
        Assert.NotEqual(lateId, freshId);
    }

    [Fact]
    public async Task ChangingSeverity_ReassessesTheSla()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var (id, _) = await CreateAsync(client, severity: "Low");

        var detail = await GetDetailAsync(client, id);
        Assert.Equal("Pending", detail.GetProperty("responseSla").GetString());

        // Menaikkan ke Critical tidak langsung membuatnya terlambat (baru saja dibuat),
        // tapi batasnya sekarang jauh lebih ketat: 15 menit dari waktu dibuat.
        var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/incidents/{id}")
        {
            Content = JsonContent.Create(new { severity = "Critical" })
        };
        var response = await client.SendAsync(patch);
        var body = await response.ReadJsonAsync();

        var created = body.GetProperty("createdAtUtc").GetDateTimeOffset();
        Assert.Equal(TimeSpan.FromMinutes(15), body.GetProperty("responseDueAtUtc").GetDateTimeOffset() - created);
        Assert.Equal("Pending", body.GetProperty("responseSla").GetString());
    }
}