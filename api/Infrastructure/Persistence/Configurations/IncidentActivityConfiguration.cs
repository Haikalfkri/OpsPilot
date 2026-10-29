using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;
using OpsPilot.Infrastructure.Identity;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class IncidentActivityConfiguration : IEntityTypeConfiguration<IncidentActivity>
{
    public void Configure(EntityTypeBuilder<IncidentActivity> builder)
    {
        builder.ToTable("IncidentActivities");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Message).IsRequired().HasMaxLength(500);
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");   // tipe JSON milik PostgreSQL

        builder.HasOne(x => x.Incident)
            .WithMany(i => i.Activities)
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ActorId boleh null (aksi dari sistem), jadi relasinya opsional
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.IncidentId, x.CreatedAtUtc });
    }
}