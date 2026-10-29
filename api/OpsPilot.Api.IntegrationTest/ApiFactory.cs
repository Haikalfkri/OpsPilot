using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    // Dipanggil xUnit SEBELUM tes pertama jalan
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Pengaturan dikirim lewat environment variable karena dibaca paling awal oleh aplikasi
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", " ");   // spasi = Redis dimatikan
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "kunci-rahasia-untuk-tes-integrasi-minimal-32-karakter");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development: migration + data demo dijalankan seperti di lokal
        builder.UseEnvironment("Development");
    }

    // Dipanggil xUnit setelah semua tes selesai
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    // Membuat HttpClient yang sudah login sebagai email tertentu
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email, string password = TestUsers.Password)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}