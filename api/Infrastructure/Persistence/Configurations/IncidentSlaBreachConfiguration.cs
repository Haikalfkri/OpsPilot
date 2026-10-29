using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class IncidentSlaBreachConfiguration : IEntityTypeConfiguration<IncidentSlaBreach>
{
    public void Configure(EntityTypeBuilder<IncidentSlaBreach> builder)
    {
        builder.ToTable("IncidentSlaBreaches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BreachType).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.Incident)
            .WithMany(i => i.SlaBreaches)
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Satu incident hanya boleh punya SATU catatan breach per jenis.
        // Ini yang membuat SLA worker aman dijalankan berulang kali.
        builder.HasIndex(x => new { x.IncidentId, x.BreachType }).IsUnique();
    }
}