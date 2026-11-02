using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpsPilot.Application.Sla;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.Infrastructure.Sla;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class SlaWorkerEndpointTests(ApiFactory factory)
{
    // ==================== Helper ====================

    /// <summary>Menjalankan satu siklus pengecekan secara manual (worker dimatikan di tes).</summary>
    private async Task<SlaBreachRunResult> RunDetectorAsync()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISlaBreachService>();
        return await service.DetectAndRecordAsync(CancellationToken.None);
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

    private static async Task<List<JsonElement>> TimelineAsync(HttpClient client, Guid incidentId)
    {
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/incidents/{incidentId}/activities?pageSize=100");
        return body.GetProperty("items").EnumerateArray().ToList();
    }

    private static List<JsonElement> OfType(List<JsonElement> items, string type) =>
        items.Where(i => i.GetProperty("type").GetString() == type).ToList();

    private static async Task<List<JsonElement>> NotificationsForAsync(HttpClient client, Guid incidentId, string type)
    {
        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications?pageSize=100");
        return body.GetProperty("items").EnumerateArray()
            .Where(i => i.GetProperty("incidentId").ValueKind == JsonValueKind.String
                        && i.GetProperty("incidentId").GetGuid() == incidentId
                        && i.GetProperty("type").GetString() == type)
            .ToList();
    }

    // ==================== Tes ====================

    [Fact]
    public async Task TheBackgroundWorker_IsSwitchedOffInTests()
    {
        var hostedServices = factory.Services.GetServices<IHostedService>();

        Assert.DoesNotContain(hostedServices, s => s is SlaBreachWorker);
    }

    [Fact]
    public async Task OverdueResponse_OnAnUnassignedIncident_AddsASystemActivity_AndNotifiesTheAdmin()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var incidentId = await TestIncidents.CreateAsync(admin);
        await MakeDueInThePastAsync(incidentId, response: true, resolution: false);

        await RunDetectorAsync();

        // Timeline: dicatat oleh "System"
        var breach = Assert.Single(OfType(await TimelineAsync(admin, incidentId), "ResponseSlaBreached"));
        Assert.Equal(JsonValueKind.Null, breach.GetProperty("actorId").ValueKind);
        Assert.Equal("System", breach.GetProperty("actorName").GetString());

        // Hanya target respons yang dilanggar
        Assert.Empty(OfType(await TimelineAsync(admin, incidentId), "ResolutionSlaBreached"));

        // Admin tim mendapat notifikasi
        Assert.Single(await NotificationsForAsync(admin, incidentId, "ResponseSlaBreached"));
    }

    [Fact]
    public async Task OverdueResolution_IsRecordedAsAResolutionBreach()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var incidentId = await TestIncidents.CreateAsync(admin);

        // Direspons tepat waktu, tapi penyelesaian sudah lewat batas
        await TestIncidents.ChangeStatusAsync(admin, incidentId, "Investigating");
        await MakeDueInThePastAsync(incidentId, response: false, resolution: true);

        await RunDetectorAsync();

        var timeline = await TimelineAsync(admin, incidentId);
        Assert.Single(OfType(timeline, "ResolutionSlaBreached"));
        Assert.Empty(OfType(timeline, "ResponseSlaBreached"));
    }

    [Fact]
    public async Task OverdueIncident_WithAnAssignee_NotifiesTheAssignee_NotTheAdmin()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var responder = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(admin);
        await admin.PutAsJsonAsync($"/api/v1/incidents/{incidentId}/assignee", new { assigneeId = TestUsers.ResponderId });
        await MakeDueInThePastAsync(incidentId, response: true, resolution: false);

        await RunDetectorAsync();

        Assert.Single(await NotificationsForAsync(responder, incidentId, "ResponseSlaBreached"));
        Assert.Empty(await NotificationsForAsync(admin, incidentId, "ResponseSlaBreached"));
    }

    [Fact]
    public async Task RunningTheDetectorRepeatedly_RecordsTheBreachOnlyOnce()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var incidentId = await TestIncidents.CreateAsync(admin);
        await MakeDueInThePastAsync(incidentId, response: true, resolution: false);

        await RunDetectorAsync();
        await RunDetectorAsync();
        await RunDetectorAsync();

        Assert.Single(OfType(await TimelineAsync(admin, incidentId), "ResponseSlaBreached"));
        Assert.Single(await NotificationsForAsync(admin, incidentId, "ResponseSlaBreached"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpsPilotDbContext>();
        Assert.Equal(1, await db.IncidentSlaBreaches.CountAsync(b => b.IncidentId == incidentId));
    }

    [Fact]
    public async Task AFreshIncident_IsNeverFlagged()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var incidentId = await TestIncidents.CreateAsync(admin);

        await RunDetectorAsync();

        var timeline = await TimelineAsync(admin, incidentId);
        Assert.Empty(OfType(timeline, "ResponseSlaBreached"));
        Assert.Empty(OfType(timeline, "ResolutionSlaBreached"));
    }

    [Fact]
    public async Task AResolvedIncident_IsNotScanned()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var incidentId = await TestIncidents.CreateAsync(admin);
        await MakeDueInThePastAsync(incidentId);
        await TestIncidents.ChangeStatusAsync(admin, incidentId, "Resolved");

        await RunDetectorAsync();

        var timeline = await TimelineAsync(admin, incidentId);
        Assert.Empty(OfType(timeline, "ResponseSlaBreached"));
        Assert.Empty(OfType(timeline, "ResolutionSlaBreached"));
    }

    [Fact]
    public async Task TheDatabase_RejectsASecondBreachRecordOfTheSameTypeForTheSameIncident()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Responder);
        var incidentId = await TestIncidents.CreateAsync(client);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpsPilotDbContext>();
        var now = DateTimeOffset.UtcNow;

        db.IncidentSlaBreaches.Add(IncidentSlaBreach.Create(incidentId, SlaBreachType.Response, now, now));
        await db.SaveChangesAsync();

        // Pengaman terakhir: constraint unik di PostgreSQL menolak duplikat
        db.IncidentSlaBreaches.Add(IncidentSlaBreach.Create(incidentId, SlaBreachType.Response, now, now));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}