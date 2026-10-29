using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpsPilot.Infrastructure.Persistence.Seed;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[Collection("api")]
public class UserAdminEndpointTests(ApiFactory factory)
{
    private static string NewEmail() => $"baru-{Guid.NewGuid():N}@opspilot.local";

    private static object ValidCreateBody(string email, string role = "Responder") =>
        new { email, displayName = "User Baru", password = "Secret123!", role };

    private static Task<HttpResponseMessage> PatchAsync(HttpClient client, string url, object body) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body) });

    private static async Task<(Guid Id, string Email)> CreateUserAsync(HttpClient admin, string role = "Responder")
    {
        var email = NewEmail();
        var response = await admin.PostAsJsonAsync("/api/v1/users", ValidCreateBody(email, role));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("id").GetGuid(), email);
    }

    // ---------- 401 dan 403 ----------

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/users", ValidCreateBody(NewEmail()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(TestUsers.Responder)]
    [InlineData(TestUsers.Viewer)]
    public async Task Create_AsNonAdmin_Returns403(string email)
    {
        var client = await factory.CreateAuthenticatedClientAsync(email);

        var response = await client.PostAsJsonAsync("/api/v1/users", ValidCreateBody(NewEmail()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(TestUsers.Responder)]
    [InlineData(TestUsers.Viewer)]
    public async Task Update_AsNonAdmin_Returns403(string email)
    {
        var client = await factory.CreateAuthenticatedClientAsync(email);

        var response = await PatchAsync(client, $"/api/v1/users/{Guid.NewGuid()}", new { displayName = "X" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- POST /users ----------

    [Fact]
    public async Task Create_AsAdmin_Returns201_InTheAdminsTeam_AndTheNewUserCanLogin()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamAdmin);
        var email = NewEmail();

        var response = await admin.PostAsJsonAsync("/api/v1/users", ValidCreateBody(email));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);

        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal("Responder", body.GetProperty("role").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());

        var me = await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Equal(me.GetProperty("teamId").GetString(), body.GetProperty("teamId").GetString());

        var newUserClient = await factory.CreateAuthenticatedClientAsync(email, "Secret123!");
        var newUserMe = await newUserClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, newUserMe.StatusCode);
    }

    [Fact]
    public async Task Create_IgnoresATeamIdSentByTheClient()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamAdmin);

        // client mencoba memasukkan user ke tim Platform
        var response = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            email = NewEmail(),
            displayName = "Penyusup",
            password = "Secret123!",
            role = "Viewer",
            teamId = DevelopmentDataSeeder.PlatformTeamId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(DevelopmentDataSeeder.OtherTeamId, body.GetProperty("teamId").GetGuid());
    }

    [Fact]
    public async Task Create_WithExistingEmail_Returns409()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamAdmin);
        var email = NewEmail();

        var first = await admin.PostAsJsonAsync("/api/v1/users", ValidCreateBody(email));
        var second = await admin.PostAsJsonAsync("/api/v1/users", ValidCreateBody(email));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidBody_Returns400WithFieldErrors()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamAdmin);

        // role sengaja tidak dikirim
        var response = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            email = "bukan-email",
            displayName = "",
            password = "lemah"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("displayName", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.True(errors.TryGetProperty("role", out _));
    }

    // ---------- GET /users/{id} ----------

    [Fact]
    public async Task GetById_InOwnTeam_Returns200()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamAdmin);
        var (id, email) = await CreateUserAsync(admin);

        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/users/{id}");

        Assert.Equal(email, body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task GetById_UserOfAnotherTeam_Returns404()
    {
        var platformAdmin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);

        var response = await platformAdmin.GetAsync($"/api/v1/users/{TestUsers.OtherTeamResponderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithNonGuidId_Returns404()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);

        var response = await admin.GetAsync("/api/v1/users/bukan-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- PATCH /users/{id} ----------

    [Fact]
    public async Task Update_ChangeRoleAndName_Returns200_AndOnlyThoseFieldsChange()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamAdmin);
        var (id, email) = await CreateUserAsync(admin, role: "Responder");

        var response = await PatchAsync(admin, $"/api/v1/users/{id}", new { role = "Viewer", displayName = "Nama Baru" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Viewer", body.GetProperty("role").GetString());
        Assert.Equal("Nama Baru", body.GetProperty("displayName").GetString());
        Assert.Equal(email, body.GetProperty("email").GetString());     // tidak berubah
        Assert.True(body.GetProperty("isActive").GetBoolean());          // tidak berubah
    }

    [Fact]
    public async Task Update_Deactivate_BlocksLoginAndExistingTokens()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamAdmin);
        var (id, email) = await CreateUserAsync(admin);

        // User sudah login dan memegang token yang sah
        var userClient = await factory.CreateAuthenticatedClientAsync(email, "Secret123!");
        Assert.Equal(HttpStatusCode.OK, (await userClient.GetAsync("/api/v1/auth/me")).StatusCode);

        var patch = await PatchAsync(admin, $"/api/v1/users/{id}", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        // Token lama langsung ditolak
        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/api/v1/auth/me")).StatusCode);

        // Login baru juga ditolak
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Secret123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Update_Reactivate_AllowsLoginAgain()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamAdmin);
        var (id, email) = await CreateUserAsync(admin);

        await PatchAsync(admin, $"/api/v1/users/{id}", new { isActive = false });
        await PatchAsync(admin, $"/api/v1/users/{id}", new { isActive = true });

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Secret123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Update_DeactivatingTheLastActiveAdmin_Returns409()
    {
        // Tim Platform hanya punya satu Admin aktif: admin@opspilot.local
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var myId = me.GetProperty("id").GetGuid();

        var response = await PatchAsync(admin, $"/api/v1/users/{myId}", new { isActive = false });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Akun tidak berubah: masih bisa dipakai
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Update_DemotingTheLastActiveAdmin_Returns409()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var myId = me.GetProperty("id").GetGuid();

        var response = await PatchAsync(admin, $"/api/v1/users/{myId}", new { role = "Viewer" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_UserOfAnotherTeam_Returns404()
    {
        var platformAdmin = await factory.CreateAuthenticatedClientAsync(TestUsers.Admin);

        var response = await PatchAsync(platformAdmin,
            $"/api/v1/users/{TestUsers.OtherTeamResponderId}", new { isActive = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithEmptyBody_Returns400()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(TestUsers.OtherTeamAdmin);
        var (id, _) = await CreateUserAsync(admin);

        var response = await PatchAsync(admin, $"/api/v1/users/{id}", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}