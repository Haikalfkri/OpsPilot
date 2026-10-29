using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Users;
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
        Task<PagedResult<TeamMemberDto>> ListMembersAsync(ListUsersQuery query, CancellationToken ct);
    }
    public sealed class TeamService(
        IApplicationDbContext db,
        IValidator<ListUsersQuery> validator,
        IIdentityService identity,
        ICurrentUser currentUser) : ITeamsService
    {
        public async Task<TeamDto> GetCurrentAsync(CancellationToken ct) =>
            await db.Teams.AsNoTracking()
                .Where(t => t.Id == currentUser.TeamId)
                .Select(t => new TeamDto(t.Id, t.Name))
                .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Team not found.");

        public async Task<PagedResult<TeamMemberDto>> ListMembersAsync(ListUsersQuery query, CancellationToken ct)
        {
            await validator.ValidateAndThrowAsync(query, ct);

            var activeOnly = query with { IsActive = true };

            var page = await identity.ListTeamUsersAsync(currentUser.TeamId, activeOnly, ct);
            return page.Map(u => u.ToMemberDto());
        }
    }
}
