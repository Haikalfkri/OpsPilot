using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Teams
{
    public sealed record TeamDto(Guid Id, string Name);
    public interface ITeamsService
    {
        Task<TeamDto> GetCurrentAsync(CancellationToken ct);
    }
    public sealed class TeamService(IApplicationDbContext db, ICurrentUser currentUser) : ITeamsService
    {
        public async Task<TeamDto> GetCurrentAsync(CancellationToken ct) =>
            await db.Teams.AsNoTracking()
                .Where(t => t.Id == currentUser.TeamId)
                .Select(t => new TeamDto(t.Id, t.Name))
                .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Team not found.");
    }
}
