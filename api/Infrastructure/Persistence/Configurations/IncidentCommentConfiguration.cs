using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;
using OpsPilot.Infrastructure.Identity;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class IncidentCommentConfiguration : IEntityTypeConfiguration<IncidentComment>
{
    public void Configure(EntityTypeBuilder<IncidentComment> builder)
    {
        builder.ToTable("IncidentComments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Body).IsRequired().HasMaxLength(2000);

        builder.HasOne(x => x.Incident)
            .WithMany(i => i.Comments)
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.IncidentId, x.CreatedAtUtc });
    }
}