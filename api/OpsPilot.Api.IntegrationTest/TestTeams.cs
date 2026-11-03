using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Identity;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

internal static class TestTeams
{
    /// <summary>
    /// Membuat TIM BARU yang kosong dan satu Admin di dalamnya, lalu mengembalikan HttpClient
    /// yang sudah login sebagai Admin itu. Semua tes berbagi satu database, jadi di tim bersama
    /// angka dashboard berubah-ubah. Di tim baru, angkanya bisa dipastikan.
    /// </summary>
    public static async Task<HttpClient> CreateAdminClientAsync(ApiFactory factory)
    {
        var teamId = Guid.NewGuid();
        var email = $"dash-{Guid.NewGuid():N}@opspilot.local";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OpsPilotDbContext>();
            db.Teams.Add(new Team { Id = teamId, Name = $"Team {teamId:N}", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Isolated Admin",
                Role = UserRole.Admin,
                TeamId = teamId,
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var result = await users.CreateAsync(admin, TestUsers.Password);
            Assert.True(result.Succeeded, "Could not create the isolated admin.");
        }

        return await factory.CreateAuthenticatedClientAsync(email);
    }
}