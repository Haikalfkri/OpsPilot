using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;

namespace OpsPilot.Application.Common.Extensions;

public static class DbContextExtensions
{
    public static async Task EnsureIncidentInTeamAsync(
        this IApplicationDbContext db, Guid incidentId, Guid teamId, CancellationToken ct)
    {
        var exists = await db.Incidents
            .AsNoTracking()
            .AnyAsync(i => i.Id == incidentId && i.TeamId == teamId, ct);

        if (!exists)
            throw new NotFoundException("Incident not found.");
    }
}