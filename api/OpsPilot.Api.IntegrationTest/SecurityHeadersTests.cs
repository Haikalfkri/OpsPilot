using System.Net.Http.Json;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class SecurityHeadersTests(ApiFactory factory)
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/tidak-ada")]
    public async Task EveryResponse_CarriesTheSecurityHeaders(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.True(response.Headers.CacheControl?.NoStore, "Responses must not be cached");
    }

    [Fact]
    public async Task ARejectedLogin_IsAlsoNotCached()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" });

        Assert.True(response.Headers.CacheControl?.NoStore);
    }
}