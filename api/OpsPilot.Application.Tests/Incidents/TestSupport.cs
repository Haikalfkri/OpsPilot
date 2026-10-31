using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Tests.Incidents;

// DbContext tiruan (database di memori) untuk menguji service tanpa PostgreSQL
public sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentComment> IncidentComments => Set<IncidentComment>();
    public DbSet<IncidentActivity> IncidentActivities => Set<IncidentActivity>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<IncidentSlaBreach> IncidentSlaBreaches => Set<IncidentSlaBreach>();
    public DbSet<Postmortem> Postmortems => Set<Postmortem>();

    // Setiap tes mendapat database kosong sendiri (nama acak)
    public static TestDbContext CreateNew()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options);
    }
}

// Jam palsu: selalu mengembalikan waktu yang sama, supaya hasil tes bisa dipastikan
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}