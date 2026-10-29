using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;
using OpsPilot.Infrastructure.Identity;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class PostmortemConfiguration : IEntityTypeConfiguration<Postmortem>
{
    public void Configure(EntityTypeBuilder<Postmortem> builder)
    {
        builder.ToTable("Postmortems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Summary).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.RootCause).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.Impact).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.CorrectiveActions).IsRequired().HasMaxLength(4000);

        // Relasi satu-ke-satu: satu incident maksimal satu postmortem
        builder.HasOne(x => x.Incident)
            .WithOne(i => i.Postmortem)
            .HasForeignKey<Postmortem>(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IncidentId).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}