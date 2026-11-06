using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class CorsTests(ApiFactory factory)
{
    private const string FrontendOrigin = "http://localhost:5173";

    [Fact]
    public async Task ErrorResponses_StillCarryTheCorsHeader_SoTheBrowserLetsTheFrontendReadThem()
    {
        // Permintaan login dengan body kosong menghasilkan 400 lewat penangan exception
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "", password = "" })
        };
        request.Headers.Add("Origin", FrontendOrigin);

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Equal(FrontendOrigin, Assert.Single(origins!));
    }

    [Fact]
    public async Task Preflight_ForAnAuthorizedJsonRequest_IsAnsweredForTheFrontendOrigin()
    {
        // Browser mengirim OPTIONS ("izinkan aku?") sebelum permintaan dengan header Authorization
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/incidents");
        request.Headers.Add("Origin", FrontendOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, $"Preflight returned {(int)response.StatusCode}");
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Equal(FrontendOrigin, Assert.Single(origins!));
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));
    }

    [Fact]
    public async Task AnUnknownOrigin_GetsNoCorsHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://evil.example");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}