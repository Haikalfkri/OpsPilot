using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Infrastructure.Persistence.Seed;

namespace OpsPilot.Infrastructure.Persistence;

public static class DatabaseInitialiser
{
    public static async Task InitialiseDatabaseAsync(
        this IServiceProvider services, bool seedDemoData, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<OpsPilotDbContext>();
        await db.Database.MigrateAsync(ct);

        if (seedDemoData)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
            await seeder.SeedAsync(ct);
        }
    }
}
