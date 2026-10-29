using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Identity;

namespace OpsPilot.Infrastructure.Persistence.Seed;

public sealed class DevelopmentDataSeeder(
    OpsPilotDbContext db,
    UserManager<ApplicationUser> users,
    TimeProvider clock,
    ILogger<DevelopmentDataSeeder> logger)
{
    public static readonly Guid PlatformTeamId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OtherTeamId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public const string DemoPassword = "DemoPassword123!";

    private sealed record SeedUser(
        Guid Id, string Email, string DisplayName, UserRole Role, Guid TeamId, bool IsActive = true);

    private static readonly SeedUser[] SeedUsers =
    {
        new(Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            "admin@opspilot.local", "Demo Admin", UserRole.Admin, PlatformTeamId),

        new(Guid.Parse("a0000000-0000-0000-0000-000000000002"),
            "responder@opspilot.local", "Demo Responder", UserRole.Responder, PlatformTeamId),

        new(Guid.Parse("a0000000-0000-0000-0000-000000000003"),
            "viewer@opspilot.local", "Demo Viewer", UserRole.Viewer, PlatformTeamId),

        new(Guid.Parse("a0000000-0000-0000-0000-000000000004"),
            "inactive@opspilot.local", "Demo Inactive", UserRole.Responder, PlatformTeamId, IsActive: false),

        // User dari tim lain, dipakai untuk menguji bahwa data antar tim tidak bocor
        new(Guid.Parse("b0000000-0000-0000-0000-000000000001"),
            "other.responder@opspilot.local", "Other Team Responder", UserRole.Responder, OtherTeamId),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        await EnsureTeamAsync(PlatformTeamId, "Platform Team", now, ct);
        await EnsureTeamAsync(OtherTeamId, "Other Team", now, ct);
        await db.SaveChangesAsync(ct);

        foreach (var seed in SeedUsers)
        {
            // Sudah ada? lewati (supaya aman dijalankan berkali-kali)
            if (await users.FindByEmailAsync(seed.Email) is not null)
                continue;

            var user = new ApplicationUser
            {
                Id = seed.Id,
                UserName = seed.Email,
                Email = seed.Email,
                EmailConfirmed = true,
                DisplayName = seed.DisplayName,
                Role = seed.Role,
                TeamId = seed.TeamId,
                IsActive = seed.IsActive,
                CreatedAtUtc = now
            };

            var result = await users.CreateAsync(user, DemoPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Code));
                throw new InvalidOperationException($"Gagal membuat user {seed.Email}: {errors}");
            }
        }

        logger.LogInformation("Seed data development selesai.");
    }

    private async Task EnsureTeamAsync(Guid id, string name, DateTimeOffset now, CancellationToken ct)
    {
        var exists = await db.Teams.AnyAsync(t => t.Id == id, ct);
        if (exists) return;

        db.Teams.Add(new Team { Id = id, Name = name, CreatedAt = now });
    }
}