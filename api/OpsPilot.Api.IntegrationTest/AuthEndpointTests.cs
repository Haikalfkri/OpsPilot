using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Identity;
using OpsPilot.Infrastructure.Persistence.Seed;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

// CATATAN: 5 kali salah password mengunci akun 15 menit.
// Jaga agar tiap user hanya gagal login sedikit kali di seluruh tes.
[Collection("api")]
public class AuthEndpointTests(ApiFactory factory)
{
    // ---------- POST /api/v1/auth/login ----------

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokenAndUser()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = TestUsers.Responder, password = TestUsers.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        Assert.Equal("Responder", body.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_Response_NeverContainsPasswordData()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = TestUsers.Responder, password = TestUsers.Password });
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = TestUsers.Viewer, password = "PasswordSalah123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsExactlyTheSameErrorAsWrongPassword()
    {
        var client = factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = TestUsers.Viewer, password = "PasswordSalah123!" });
        var unknownEmail = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "tidak.ada@opspilot.local", password = "PasswordSalah123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        var a = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var b = await unknownEmail.Content.ReadFromJsonAsync<JsonElement>();

        // Pesan identik: penyerang tidak bisa menebak email mana yang terdaftar
        Assert.Equal(a.GetProperty("title").GetString(), b.GetProperty("title").GetString());
        Assert.Equal(a.GetProperty("detail").GetString(), b.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_WithInactiveUser_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = TestUsers.Inactive, password = TestUsers.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyFields_Returns400WithFieldErrors()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Validation failed", body.GetProperty("title").GetString());

        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    // ---------- GET /api/v1/auth/me ----------

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithGarbageToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "ini.bukan.token");

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsTheSignedInUser()
    {
        var client = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");

        Assert.Equal(TestUsers.Admin, body.GetProperty("email").GetString());
        Assert.Equal("Admin", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Me_AfterTheUserIsDeactivated_Returns401EvenWithAValidToken()
    {
        // Arrange: buat user khusus untuk tes ini (di tim lain supaya tes lain tidak terganggu)
        var email = $"sementara-{Guid.NewGuid():N}@opspilot.local";
        await CreateUserAsync(email);

        var client = await factory.CreateAuthenticatedClientAsync(email);
        var before = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // Act: Admin menonaktifkan user ini
        await DeactivateUserAsync(email);

        // Assert: token masih sah secara teknis, tapi user nonaktif -> ditolak
        var after = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    // ---------- Helper ----------

    private async Task CreateUserAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "User Sementara",
            Role = UserRole.Responder,
            TeamId = DevelopmentDataSeeder.OtherTeamId,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await users.CreateAsync(user, TestUsers.Password);
        Assert.True(result.Succeeded);
    }

    private async Task DeactivateUserAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByEmailAsync(email);
        user!.IsActive = false;
        await users.UpdateAsync(user);
    }
}