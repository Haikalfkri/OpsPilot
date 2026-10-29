using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Entities;
using OpsPilot.Infrastructure.Identity;

namespace OpsPilot.Infrastructure.Persistence;

public class OpsPilotDbContext(DbContextOptions<OpsPilotDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IApplicationDbContext
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentComment> IncidentComments => Set<IncidentComment>();
    public DbSet<IncidentActivity> IncidentActivities => Set<IncidentActivity>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<IncidentSlaBreach> IncidentSlaBreaches => Set<IncidentSlaBreach>();
    public DbSet<Postmortem> Postmortems => Set<Postmortem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Beri nama tabel Identity yang lebih rapi (default-nya "AspNetUserClaims", dst.)
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        // Muat semua file konfigurasi di folder Configurations
        builder.ApplyConfigurationsFromAssembly(typeof(OpsPilotDbContext).Assembly);
    }
}