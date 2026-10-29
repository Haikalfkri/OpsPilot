using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;
using OpsPilot.Infrastructure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Infrastructure.Persistence.Configurations
{
    public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
    {
        public void Configure(EntityTypeBuilder<ApplicationUser> builder)
        {
            builder.ToTable("Users");

            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(120);
            builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(x => x.CreatedAtUtc).IsRequired();

            // Satu email hanya boleh dipakai satu user
            builder.HasIndex(x => x.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
            builder.HasIndex(x => x.TeamId);

            // Setiap user milik satu tim; tim tidak boleh dihapus kalau masih punya user
            builder.HasOne<Team>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
