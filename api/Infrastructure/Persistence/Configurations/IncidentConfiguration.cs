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
    public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
    {
        public void Configure(EntityTypeBuilder<Incident> builder)
        {
            builder.ToTable("Incidents");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Title).IsRequired().HasMaxLength(160);
            builder.Property(x => x.Description).IsRequired().HasMaxLength(5000);
            builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            // Relasi: tim, pembuat, dan assignee. Pembuat dan assignee sama-sama menunjuk ke tabel Users,
            // jadi masing-masing harus ditulis eksplisit.
            builder.HasOne<Team>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index untuk filter yang paling sering dipakai (selalu diawali TeamId)
            builder.HasIndex(x => new { x.TeamId, x.Status, x.CreatedAtUtc });
            builder.HasIndex(x => new { x.TeamId, x.Severity, x.CreatedAtUtc });
            builder.HasIndex(x => new { x.TeamId, x.AssigneeId, x.Status });
            builder.HasIndex(x => new { x.TeamId, x.ResolutionDueAtUtc, x.Status });
        }
    }
}
