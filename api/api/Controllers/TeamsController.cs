using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Api.Auth;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Teams;
using OpsPilot.Application.Users;

namespace OpsPilot.Api.Controllers;

public sealed class TeamsController(ITeamsService teamService) : ApiControllerBase
{
    [Authorize(Policy = Policies.AnyTeamMember)]
    [HttpGet("current")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamDto>> GetCurrent(CancellationToken cancellationToken)
    {
        var team = await teamService.GetCurrentAsync(cancellationToken);
        return Ok(team);
    }

    [Authorize(Policy = Policies.AnyTeamMember)]
    [HttpGet("current/users")]
    [ProducesResponseType(typeof(PagedResult<TeamMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TeamMemberDto>>> ListMembers(
        [FromQuery] ListUsersQuery query,
        CancellationToken cancellationToken)
    {
        var members = await teamService.ListMembersAsync(query, cancellationToken);
        return Ok(members);
    }
}