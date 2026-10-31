using System.Net.Http.Json;
using System.Text.Json;

namespace OpsPilot.Api.IntegrationTests;

internal static class TestHttp
{
    // Mengirim request PATCH dengan body JSON
    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object body) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body) });

    // Membaca body response sebagai JSON
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}