using Microsoft.EntityFrameworkCore;
using OpsPilot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Team> Teams { get; }
        DbSet<Incident> Incidents { get; }
        DbSet<IncidentComment> IncidentComments { get; }
        DbSet<IncidentActivity> IncidentActivities { get; }
        DbSet<Notification> Notifications { get; }
        DbSet<IncidentSlaBreach> IncidentSlaBreaches { get; }
        DbSet<Postmortem> Postmortems { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
