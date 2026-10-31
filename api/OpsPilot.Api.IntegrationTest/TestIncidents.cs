using System.Net.Http.Json;

namespace OpsPilot.Api.IntegrationTests;

internal static class TestIncidents
{
    public static async Task<Guid> CreateAsync(HttpClient client, string severity = "High")
    {
        var response = await client.PostAsJsonAsync("/api/v1/incidents", new
        {
            title = $"Timeline test {Guid.NewGuid():N}",
            description = "Dibuat untuk pengujian timeline dan komentar",
            severity
        });
        response.EnsureSuccessStatusCode();

        var body = await response.ReadJsonAsync();
        return body.GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> AddCommentAsync(HttpClient client, Guid incidentId, string body) =>
        client.PostAsJsonAsync($"/api/v1/incidents/{incidentId}/comments", new { body });

    public static Task<HttpResponseMessage> ChangeStatusAsync(HttpClient client, Guid incidentId, string status) =>
        client.PostAsJsonAsync($"/api/v1/incidents/{incidentId}/status", new { status });
}